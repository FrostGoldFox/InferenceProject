"""CreateModelToUseCheckpoint로 생성한 모델을 이용해 이미지 한 장을 검사한다."""

import argparse
from pathlib import Path

import torch
from PIL import Image
from torchvision.transforms import functional as F

from CreateModelToUseCheckpoint import create_model_from_checkpoint, DEFAULT_CHECKPOINT_PATH


def inspect_image(image_path, checkpoint_path=DEFAULT_CHECKPOINT_PATH, score_threshold=0.5):
    model, payload, class_to_id, device = create_model_from_checkpoint(checkpoint_path)
    id_to_class = {v: k for k, v in class_to_id.items()}

    image = Image.open(image_path).convert("RGB")
    tensor = F.to_tensor(image).to(device)

    with torch.no_grad():
        prediction = model([tensor])[0]

    boxes = prediction["boxes"].cpu()
    labels = prediction["labels"].cpu()
    scores = prediction["scores"].cpu()

    results = []
    for box, label, score in zip(boxes, labels, scores):
        if score.item() < score_threshold:
            continue
        results.append({
            "class": id_to_class.get(label.item(), f"unknown_id_{label.item()}"),
            "score": score.item(),
            "box": [round(v, 1) for v in box.tolist()],
        })

    return results


def parse_args():
    parser = argparse.ArgumentParser(description="checkpoint 모델로 이미지 한 장을 검사한다.")
    parser.add_argument("image", help="검사할 이미지 경로")
    parser.add_argument("--checkpoint", default=str(DEFAULT_CHECKPOINT_PATH))
    parser.add_argument("--score-threshold", type=float, default=0.5)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    results = inspect_image(Path(args.image), args.checkpoint, args.score_threshold)

    if not results:
        print("검출 결과 없음 (임계값 이상 없음)")
    for r in results:
        print(f"class={r['class']}  score={r['score']:.4f}  box={r['box']}")
