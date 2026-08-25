import argparse
import json
from pathlib import Path

from ultralytics import YOLO


ROOT = Path(__file__).resolve().parent
MODELS = {
    "V1": ROOT / "models" / "v1.pt",
    "V2": ROOT / "models" / "v2.pt",
}


def response(success, model, detections=None, error=""):
    print(json.dumps({
        "success": success,
        "model": model,
        "detections": detections or [],
        "error": error,
    }))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--image", required=True)
    parser.add_argument("--model", default="V1", choices=["V1", "V2"])
    parser.add_argument("--confidence", type=float, default=0.25)
    args = parser.parse_args()

    image_path = Path(args.image)

    if not image_path.exists():
        response(False, args.model, error=f"Image not found: {image_path}")
        return 2

    model_path = MODELS[args.model]

    if not model_path.exists():
        response(
            False,
            args.model,
            error=(
                f"Model {args.model} is not installed.\n"
                f"Put the trained YOLO weights here:\n{model_path}"
            ),
        )
        return 3

    try:
        model = YOLO(str(model_path))

        results = model.predict(
            source=str(image_path),
            conf=args.confidence,
            verbose=False,
        )

        detections = []

        for result in results:
            if result.boxes is None:
                continue

            names = result.names

            for box in result.boxes:
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                confidence = float(box.conf[0])
                class_id = int(box.cls[0])

                class_name = str(names[class_id])

                detections.append({
                    "className": class_name,
                    "confidence": confidence,
                    "x1": int(x1),
                    "y1": int(y1),
                    "x2": int(x2),
                    "y2": int(y2),
                })

        response(True, args.model, detections=detections)
        return 0

    except Exception as exc:
        response(False, args.model, error=str(exc))
        return 10


if __name__ == "__main__":
    raise SystemExit(main())
