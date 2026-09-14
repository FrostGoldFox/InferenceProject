"""DATA_TRAINING_METHOD.md 기준 학습된 checkpoint로 Faster R-CNN MobileNet V3 Large FPN 모델을 생성한다."""

from pathlib import Path

import torch
from torchvision.models import detection as tvdet

DEFAULT_CHECKPOINT_PATH = Path(__file__).resolve().with_name("checkpoint.pt")

ARCHITECTURE_BUILDERS = {
    "fasterrcnn_mobilenet": tvdet.fasterrcnn_mobilenet_v3_large_fpn,
    "fasterrcnn_mobilenet_v3_large_fpn": tvdet.fasterrcnn_mobilenet_v3_large_fpn,
}


def load_checkpoint(checkpoint_path=DEFAULT_CHECKPOINT_PATH):
    return torch.load(Path(checkpoint_path), map_location="cpu", weights_only=False)


def resolve_device(device):
    if device == "auto":
        return torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
    return torch.device(device)


def apply_bn_mode(model, payload, bn_mode):
    recalibrated = payload.get("bn_recalibrated")
    if bn_mode == "eval" or (bn_mode == "auto" and recalibrated):
        return
    for module in model.modules():
        if isinstance(module, torch.nn.BatchNorm2d):
            module.train()
            module.momentum = 0.0


def create_model_from_checkpoint(checkpoint_path=DEFAULT_CHECKPOINT_PATH, device="auto", bn_mode="auto"):
    """checkpoint를 읽어 학습 당시와 동일한 구조의 모델을 만들고 가중치를 로드한다."""
    payload = load_checkpoint(checkpoint_path)

    train_args = payload.get("args") or {}
    architecture = train_args.get("model", "fasterrcnn_mobilenet")
    builder = ARCHITECTURE_BUILDERS.get(architecture)
    if builder is None:
        raise ValueError(f"지원하지 않는 구조입니다. message : {architecture}")

    class_to_id = payload.get("class_to_id") or {}
    num_classes = len(class_to_id) + 1  # 배경(ID 0) 포함
    min_size = int(train_args.get("min_size", 960))
    max_size = int(train_args.get("max_size", 1280))

    model = builder(
        weights=None,
        weights_backbone=None,
        num_classes=num_classes,
        min_size=min_size,
        max_size=max_size,
    )
    model.load_state_dict(payload["model"])

    resolved_device = resolve_device(device)
    model.to(resolved_device).eval()
    apply_bn_mode(model, payload, bn_mode)

    return model, payload, class_to_id, resolved_device


if __name__ == "__main__":
    model, payload, class_to_id, device = create_model_from_checkpoint()
    print(f"모델 생성 완료 - device={device}, epoch={payload.get('epoch')}, num_classes={len(class_to_id) + 1}")
