from contextlib import asynccontextmanager, suppress
from dataclasses import dataclass
from datetime import datetime, timezone
from io import BytesIO
import asyncio
import base64
import binascii
import logging
import os
import json
import time
from urllib import request as urllib_request

from fastapi import FastAPI, HTTPException, Request, status
from PIL import Image, UnidentifiedImageError
import torch
from torchvision.transforms import functional as F

from CreateModelToUseCheckpoint import (
    DEFAULT_CHECKPOINT_PATH,
    create_model_from_checkpoint,
)


logger = logging.getLogger("uvicorn.error")
MAX_IMAGE_BYTES = 20 * 1024 * 1024
MAX_ENCODED_IMAGE_CHARS = ((MAX_IMAGE_BYTES + 2) // 3) * 4
MAX_JSON_BYTES = MAX_ENCODED_IMAGE_CHARS + (64 * 1024)
MAX_IMAGE_PIXELS = 25_000_000
QUEUE_CAPACITY = 32
MIDDLEWARE_SEND_ATTEMPTS = 3


@dataclass(slots=True)
class InferenceRuntime:
    model: torch.nn.Module
    id_to_class: dict[int, str]
    device: torch.device
    score_threshold: float

    @classmethod
    def load(cls) -> "InferenceRuntime":
        checkpoint_path = os.getenv(
            "INFERENCE_CHECKPOINT_PATH",
            str(DEFAULT_CHECKPOINT_PATH),
        )
        device_name = os.getenv("INFERENCE_DEVICE", "auto")
        score_threshold = float(os.getenv("INFERENCE_SCORE_THRESHOLD", "0.4"))

        model, _, class_to_id, device = create_model_from_checkpoint(
            checkpoint_path=checkpoint_path,
            device=device_name,
        )
        return cls(
            model=model,
            id_to_class={class_id: name for name, class_id in class_to_id.items()},
            device=device,
            score_threshold=score_threshold,
        )

    def inspect(self, image: Image.Image) -> list[dict]:
        tensor = F.to_tensor(image).to(self.device)

        with torch.inference_mode():
            prediction = self.model([tensor])[0]

        boxes = prediction["boxes"].detach().cpu()
        labels = prediction["labels"].detach().cpu()
        scores = prediction["scores"].detach().cpu()

        detections = []
        for box, label, score in zip(boxes, labels, scores):
            confidence = score.item()
            if confidence < self.score_threshold:
                continue

            class_id = label.item()
            detections.append(
                {
                    "class": self.id_to_class.get(class_id, f"unknownid{class_id}"),
                    "score": confidence,
                    "box": [round(value, 1) for value in box.tolist()],
                }
            )

        return detections


def decode_base64_image(filedata: str, expected_length: int) -> Image.Image:
    if not isinstance(filedata, str) or not filedata.strip():
        raise ValueError("filedata must be a non-empty Base64 string.")
    if not isinstance(expected_length, int) or isinstance(expected_length, bool):
        raise ValueError("filelength must be an integer.")
    if expected_length < 1:
        raise ValueError("filelength must be greater than zero.")

    encoded = filedata.strip()
    if encoded.startswith("data:"):
        _, separator, encoded = encoded.partition(",")
        if not separator:
            raise ValueError("Invalid Base64 data URL.")

    if len(encoded) > MAX_ENCODED_IMAGE_CHARS:
        raise ValueError(f"Encoded image exceeds the {MAX_IMAGE_BYTES}-byte limit.")

    try:
        image_bytes = base64.b64decode(encoded, validate=True)
    except (binascii.Error, ValueError) as exception:
        raise ValueError("filedata is not valid Base64.") from exception

    if len(image_bytes) != expected_length:
        raise ValueError(
            f"filelength does not match decoded bytes: "
            f"expected={expected_length}, actual={len(image_bytes)}"
        )
    if len(image_bytes) > MAX_IMAGE_BYTES:
        raise ValueError(f"Image exceeds the {MAX_IMAGE_BYTES}-byte limit.")

    try:
        with Image.open(BytesIO(image_bytes)) as source:
            if source.width * source.height > MAX_IMAGE_PIXELS:
                raise ValueError(
                    f"Image exceeds the {MAX_IMAGE_PIXELS}-pixel limit."
                )
            source.load()
            return source.convert("RGB")
    except (UnidentifiedImageError, OSError) as exception:
        raise ValueError("Decoded data is not a supported image.") from exception


def validate_request_message(message: dict) -> None:
    if message.get("type") != "request":
        raise ValueError("type must be request or reset.")

    client = message.get("client")
    if not isinstance(client, int) or isinstance(client, bool) or client < 1:
        raise ValueError("client must be a positive integer.")

    filename = message.get("filename")
    if not isinstance(filename, str) or not filename.strip():
        raise ValueError("filename must be a non-empty string.")


def create_main_server_message(client_id: int, detections: list[dict]) -> dict:
    if not detections:
        return {
            "ClientId": client_id,
            "ProductName": "Unknown",
            "SucessRate": "fail",
            "Confidence": None,
            "Box": None,
        }

    top_detection = max(detections, key=lambda detection: detection["score"])
    product_name, separator, _ = top_detection["class"].partition("|")
    if not separator or not product_name.strip():
        product_name = "Unknown"

    return {
        "ClientId": client_id,
        "ProductName": product_name.strip(),
        "SucessRate": "sucess",
        "Confidence": round(top_detection["score"], 4),
        "Box": top_detection["box"],
    }


def send_main_server_message(message: dict) -> None:
    destination = os.getenv(
        "MIDDLEWARE_RESPONSE_URL",
        "http://localhost:5073/ResponSE",
    )
    payload = json.dumps(message, ensure_ascii=False).encode("utf-8")
    outbound_request = urllib_request.Request(
        destination,
        data=payload,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )

    last_exception = None
    for attempt in range(1, MIDDLEWARE_SEND_ATTEMPTS + 1):
        logger.info(
            "Sending inference result. Destination=%s Attempt=%s/%s ClientId=%s ProductName=%s SucessRate=%s Confidence=%s",
            destination,
            attempt,
            MIDDLEWARE_SEND_ATTEMPTS,
            message.get("ClientId"),
            message.get("ProductName"),
            message.get("SucessRate"),
            message.get("Confidence"),
        )
        try:
            with urllib_request.urlopen(outbound_request, timeout=30) as response:
                if not 200 <= response.status < 300:
                    raise RuntimeError(
                        f"MiddleWare returned HTTP {response.status}."
                    )
                response.read()
                logger.info(
                    "Inference result sent. Destination=%s HttpStatus=%s ClientId=%s",
                    destination,
                    response.status,
                    message.get("ClientId"),
                )
                return
        except Exception as exception:
            last_exception = exception
            logger.warning(
                "Inference result send failed. Destination=%s Attempt=%s/%s ClientId=%s Error=%s",
                destination,
                attempt,
                MIDDLEWARE_SEND_ATTEMPTS,
                message.get("ClientId"),
                exception,
            )
            if attempt == MIDDLEWARE_SEND_ATTEMPTS:
                break
            delay_seconds = 0.5 * (2 ** (attempt - 1))
            logger.warning(
                "MiddleWare send failed. Attempt=%s/%s RetryInSeconds=%s",
                attempt,
                MIDDLEWARE_SEND_ATTEMPTS,
                delay_seconds,
            )
            time.sleep(delay_seconds)

    raise RuntimeError(
        f"MiddleWare send failed after {MIDDLEWARE_SEND_ATTEMPTS} attempts."
    ) from last_exception


async def process_queue(app: FastAPI) -> None:
    while True:
        message = await app.state.message_queue.get()
        try:
            if message["type"] == "reset":
                logger.info("Reset message processing started.")
                app.state.last_inference = None
                app.state.last_error = None
                logger.info("Reset message processing completed.")
                continue

            started_at = time.perf_counter()
            logger.info(
                "Inference processing started. ClientId=%s Filename=%s ImageSize=%sx%s QueueRemaining=%s",
                message["client"],
                message["filename"],
                message["image"].width,
                message["image"].height,
                app.state.message_queue.qsize(),
            )
            runtime: InferenceRuntime = app.state.runtime
            detections = await asyncio.to_thread(runtime.inspect, message["image"])
            main_server_message = create_main_server_message(
                message["client"],
                detections,
            )
            logger.info(
                "Inference result created. ClientId=%s Filename=%s Detections=%s ProductName=%s SucessRate=%s DetectionSummary=%s",
                message["client"],
                message["filename"],
                len(detections),
                main_server_message["ProductName"],
                main_server_message["SucessRate"],
                [
                    {"class": item["class"], "score": round(item["score"], 4)}
                    for item in detections
                ],
            )
            await asyncio.to_thread(
                send_main_server_message,
                main_server_message,
            )
            app.state.processed_count += 1
            app.state.last_error = None
            app.state.last_inference = {
                "ClientId": message["client"],
                "Filename": message["filename"],
                "Detections": detections,
                "MainServerMessage": main_server_message,
                "ProcessedAt": datetime.now(timezone.utc).isoformat(),
            }
            logger.info(
                "Inference processing completed. ClientId=%s Filename=%s Detections=%s DurationMs=%.1f",
                message["client"],
                message["filename"],
                len(detections),
                (time.perf_counter() - started_at) * 1000,
            )
        except Exception as exception:
            app.state.failed_count += 1
            app.state.last_error = str(exception)
            app.state.last_failed_job = {
                "ClientId": message.get("client"),
                "Filename": message.get("filename"),
                "FailedAt": datetime.now(timezone.utc).isoformat(),
            }
            logger.exception(
                "Inference failed. ClientId=%s Filename=%s",
                message.get("client"),
                message.get("filename"),
            )
        finally:
            app.state.message_queue.task_done()


@asynccontextmanager
async def lifespan(app: FastAPI):
    # 모델은 서버 시작 시 여기서 정확히 한 번 생성하고 모든 요청에서 재사용한다.
    app.state.runtime = InferenceRuntime.load()
    app.state.model_load_count = 1
    app.state.message_queue = asyncio.Queue(maxsize=QUEUE_CAPACITY)
    app.state.processed_count = 0
    app.state.failed_count = 0
    app.state.last_inference = None
    app.state.last_failed_job = None
    app.state.last_error = None

    consumer_task = asyncio.create_task(process_queue(app))
    try:
        yield
    finally:
        consumer_task.cancel()
        with suppress(asyncio.CancelledError):
            await consumer_task


app = FastAPI(title="InferenceServer", lifespan=lifespan)


@app.get("/health")
def health(request: Request):
    return {
        "status": "ready",
        "modelLoaded": hasattr(request.app.state, "runtime"),
        "modelLoadCount": request.app.state.model_load_count,
        "queuedCount": request.app.state.message_queue.qsize(),
        "processedCount": request.app.state.processed_count,
        "failedCount": request.app.state.failed_count,
        "queueCapacity": QUEUE_CAPACITY,
        "lastError": request.app.state.last_error,
        "lastFailedJob": request.app.state.last_failed_job,
    }


@app.post("/message", status_code=status.HTTP_202_ACCEPTED)
async def receive_message(request: Request):
    content_length = request.headers.get("content-length")
    if content_length is not None:
        try:
            if int(content_length) > MAX_JSON_BYTES:
                raise HTTPException(
                    status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                    detail=f"Request exceeds the {MAX_JSON_BYTES}-byte JSON limit.",
                )
        except ValueError as exception:
            raise HTTPException(
                status_code=400,
                detail="Content-Length must be an integer.",
            ) from exception

    if request.app.state.message_queue.full():
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Inference queue is full.",
        )

    try:
        body = await request.json()
    except Exception as exception:
        raise HTTPException(status_code=400, detail="Request body must be JSON.") from exception

    if not isinstance(body, dict):
        raise HTTPException(status_code=400, detail="Request body must be a JSON object.")

    logger.info(
        "Message received. Remote=%s Type=%s ClientId=%s Filename=%s ContentLength=%s",
        request.client.host if request.client else "unknown",
        body.get("type"),
        body.get("client"),
        body.get("filename"),
        content_length,
    )

    if body.get("type") == "reset":
        await request.app.state.message_queue.put({"type": "reset"})
        logger.info("Reset message queued. QueueSize=%s", request.app.state.message_queue.qsize())
        return {"status": "queued", "type": "reset"}

    try:
        validate_request_message(body)
        image = decode_base64_image(body.get("filedata"), body.get("filelength"))
        logger.info(
            "Image validated. ClientId=%s Filename=%s Bytes=%s ImageMode=%s ImageSize=%sx%s",
            body["client"],
            body["filename"],
            body["filelength"],
            image.mode,
            image.width,
            image.height,
        )
    except ValueError as exception:
        raise HTTPException(status_code=400, detail=str(exception)) from exception

    try:
        request.app.state.message_queue.put_nowait(
            {
                "type": "request",
                "client": body["client"],
                "filename": body["filename"],
                "image": image,
            }
        )
    except asyncio.QueueFull as exception:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Inference queue is full.",
        ) from exception
    logger.info(
        "Inference request queued. ClientId=%s Filename=%s QueueSize=%s/%s",
        body["client"],
        body["filename"],
        request.app.state.message_queue.qsize(),
        QUEUE_CAPACITY,
    )
    return {
        "status": "queued",
        "client": body["client"],
        "filename": body["filename"],
    }


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
