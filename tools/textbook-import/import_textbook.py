"""
LRN-01: nạp một cuốn sách giáo khoa dạng PDF cho tính năng AI soạn câu hỏi bám sách.

Việc làm (chạy một lần cho mỗi cuốn):
  1. Xuất mỗi trang PDF thành ảnh JPG (thư mục tạm sgk-source/_pages/<code>, đã gitignore)
  2. Tải ảnh lên Cloudinary: public_id msms/textbooks/<code>-p-<trang PDF>, trùng quy ước của TextbookService
  3. Xác định độ lệch giữa số trang PDF và số in ở chân trang (hoặc nhận từ --offset)
  4. Cho Gemini đọc các trang mục lục, lập bảng bài -> trang in bắt đầu / kết thúc
  5. Ghi backend/MSMS.Api/Data/Textbooks/<code>.json; API tự đọc file này vào DB mỗi lần khởi động

Ví dụ:
  python -I tools/textbook-import/import_textbook.py ^
      --pdf "sgk-source/SGK Toán 6, tập một-mau.pdf" --code toan6-t1 --subject "Toán" --grade 6 --volume 1 ^
      --title "Toán 6 – tập một" --toc-pages 5

Khóa lấy từ biến môi trường (AI_API_KEY, CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY, CLOUDINARY_API_SECRET) hoặc từ
dotnet user-secrets của MSMS.Api (Ai:ApiKey, Cloudinary:CloudName, Cloudinary:ApiKey, Cloudinary:ApiSecret).
Model lấy từ AI_MODEL hoặc Ai:Model trong appsettings.json. Khóa không bao giờ được in ra.
"""
import argparse
import base64
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pymupdf

ROOT = Path(__file__).resolve().parents[2]
API_PROJECT = ROOT / "backend" / "MSMS.Api"
BOOK_DIR = API_PROJECT / "Data" / "Textbooks"
WORK_DIR = ROOT / "sgk-source" / "_pages"
SECRETS_ID = "MSMS-Api-Iam"
MAX_LESSON_PAGES = 10
BASE_URL = "https://generativelanguage.googleapis.com/v1beta"
CLOUD_FOLDER = "msms/textbooks"


def read_secrets() -> dict:
    path = Path(os.environ.get("APPDATA", "")) / "Microsoft" / "UserSecrets" / SECRETS_ID / "secrets.json"
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {}


def secret(env: str, flat: str, secrets: dict) -> str:
    section, name = flat.split(":")
    return os.environ.get(env) or secrets.get(flat) or secrets.get(section, {}).get(name, "")


def load_ai_settings() -> tuple[str, str]:
    key = secret("AI_API_KEY", "Ai:ApiKey", read_secrets())
    model = os.environ.get("AI_MODEL", "")
    if not model:
        cfg = json.loads((API_PROJECT / "appsettings.json").read_text(encoding="utf-8-sig"))
        model = cfg.get("Ai", {}).get("Model", "")
    if not key or not model:
        sys.exit("Thiếu khóa hoặc model Gemini. Đặt AI_API_KEY hoặc chạy: dotnet user-secrets set \"Ai:ApiKey\" ...")
    return key, model


def load_cloudinary() -> tuple[str, str, str]:
    s = read_secrets()
    cloud = secret("CLOUDINARY_CLOUD_NAME", "Cloudinary:CloudName", s)
    key = secret("CLOUDINARY_API_KEY", "Cloudinary:ApiKey", s)
    sec = secret("CLOUDINARY_API_SECRET", "Cloudinary:ApiSecret", s)
    if not (cloud and key and sec):
        sys.exit("Thiếu khóa Cloudinary. Đặt dotnet user-secrets Cloudinary:CloudName, ApiKey, ApiSecret.")
    return cloud, key, sec


def gemini_json(key: str, model: str, prompt: str, images: list[bytes]) -> dict:
    parts = [{"inline_data": {"mime_type": "image/jpeg", "data": base64.b64encode(b).decode()}} for b in images]
    parts.append({"text": prompt})
    body = json.dumps({
        "contents": [{"role": "user", "parts": parts}],
        "generationConfig": {"responseMimeType": "application/json", "temperature": 0, "maxOutputTokens": 8192},
    }).encode()
    req = urllib.request.Request(
        f"{BASE_URL}/models/{model}:generateContent", data=body, method="POST",
        headers={"Content-Type": "application/json", "x-goog-api-key": key})
    for attempt in range(1, 5):
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                payload = json.load(resp)
            text = "".join(p.get("text", "") for p in payload["candidates"][0]["content"]["parts"] if not p.get("thought"))
            return json.loads(text)
        except urllib.error.HTTPError as e:
            if e.code in (429, 503) and attempt < 4:
                time.sleep(5 * attempt)
                continue
            sys.exit(f"Gemini trả lỗi {e.code}. Chạy lại sau.")
        except (urllib.error.URLError, json.JSONDecodeError, KeyError, IndexError) as e:
            if attempt < 4:
                time.sleep(3)
                continue
            sys.exit(f"Không đọc được kết quả Gemini: {type(e).__name__}")
    sys.exit("Gemini không phản hồi.")


def render_jpg(page: "pymupdf.Page", zoom: float, quality: int = 80) -> bytes:
    return page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), alpha=False).tobytes("jpg", jpg_quality=quality)


def page_name(code: str, pdf_page: int) -> str:
    """Giữ đúng quy ước TextbookService.PageFileName."""
    return f"{code}-p-{pdf_page:03d}"


def cloudinary_exists(cloud: str, name: str) -> bool:
    req = urllib.request.Request(f"https://res.cloudinary.com/{cloud}/image/upload/{CLOUD_FOLDER}/{name}.jpg", method="HEAD")
    try:
        with urllib.request.urlopen(req, timeout=30):
            return True
    except urllib.error.HTTPError:
        return False


def cloudinary_upload(cloud: str, api_key: str, api_secret: str, name: str, data: bytes) -> None:
    """Tải lên có chữ ký (REST của Cloudinary). Chữ ký = sha1 của các tham số xếp theo tên + api_secret."""
    params = {"asset_folder": CLOUD_FOLDER, "overwrite": "true", "public_id": f"{CLOUD_FOLDER}/{name}", "timestamp": str(int(time.time()))}
    to_sign = "&".join(f"{k}={v}" for k, v in sorted(params.items())) + api_secret
    fields = {**params, "api_key": api_key, "signature": hashlib.sha1(to_sign.encode()).hexdigest()}

    boundary = uuid.uuid4().hex
    body = b""
    for k, v in fields.items():
        body += f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode()
    body += (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}.jpg"\r\n'
             f"Content-Type: image/jpeg\r\n\r\n").encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(
        f"https://api.cloudinary.com/v1_1/{cloud}/image/upload", data=body, method="POST",
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    for attempt in range(1, 4):
        try:
            with urllib.request.urlopen(req, timeout=120):
                return
        except urllib.error.HTTPError as e:
            if e.code >= 500 and attempt < 3:
                time.sleep(3 * attempt)
                continue
            sys.exit(f"Cloudinary từ chối ảnh {name} (lỗi {e.code}).")
        except urllib.error.URLError:
            if attempt < 3:
                time.sleep(3 * attempt)
                continue
            sys.exit(f"Không kết nối được Cloudinary khi tải {name}.")


def detect_offset(doc, key: str, model: str) -> int:
    """Hỏi Gemini số in ở chân vài trang mẫu; mọi mẫu phải cho cùng một độ lệch."""
    n = len(doc)
    samples = sorted({max(0, n // 4), max(0, n // 2), max(0, 3 * n // 4)})
    offsets = []
    for i in samples:
        r = gemini_json(key, model,
                        'Đây là một trang sách giáo khoa. Số trang in nằm ở chân trang (góc dưới) là số mấy? '
                        'Trả về JSON {"printedPage": <số nguyên>} hoặc {"printedPage": null} nếu không thấy số.',
                        [render_jpg(doc[i], 0.6)])
        printed = r.get("printedPage")
        print(f"  trang PDF {i + 1}: số in = {printed}")
        if isinstance(printed, int):
            offsets.append((i + 1) - printed)
    if not offsets or len(set(offsets)) != 1:
        sys.exit(f"Không xác định được độ lệch số trang (các mẫu: {offsets}). Chạy lại với --offset <số>.")
    return offsets[0]


def read_toc(doc, key: str, model: str, toc_pages: list[int]) -> list[dict]:
    r = gemini_json(
        key, model,
        "Đây là trang mục lục của một sách giáo khoa. Liệt kê MỌI dòng có số trang theo đúng thứ tự trong sách, "
        "gồm cả các dòng như 'Luyện tập chung', 'Bài tập cuối chương' và các mục trong phần 'Hoạt động thực hành trải nghiệm'. "
        'Trả về JSON {"entries":[{"chapter":"<tên chương hoặc phần chứa dòng này>","title":"<tên dòng, ví dụ Bài 2. Cách ghi số tự nhiên>",'
        '"startPage":<số trang in ghi trong mục lục>,"isLesson":<true nếu là bài học hoặc luyện tập/ôn tập, false nếu là bảng tra cứu, thuật ngữ, phụ lục>}]}. '
        "Chép đúng chữ trên trang, không thêm, không bỏ dòng nào.",
        [render_jpg(doc[p - 1], 1.0) for p in toc_pages])
    # Gemini có lúc trả thẳng một mảng thay vì {"entries": [...]}.
    entries = r if isinstance(r, list) else r.get("entries")
    if not entries:
        sys.exit("Gemini không trả về dòng mục lục nào. Chạy lại hoặc kiểm tra --toc-pages.")
    return entries


def build_lessons(entries: list[dict], last_printed_page: int) -> list[dict]:
    entries = sorted(entries, key=lambda e: e["startPage"])
    lessons = []
    for i, e in enumerate(entries):
        if not e.get("isLesson"):
            continue
        end = entries[i + 1]["startPage"] - 1 if i + 1 < len(entries) else last_printed_page
        lessons.append({
            "chapter": e["chapter"].strip(), "order": len(lessons) + 1, "title": e["title"].strip(),
            "startPage": e["startPage"], "endPage": max(end, e["startPage"]),
        })
    return lessons


def pages_text(start: int, end: int) -> str:
    return f"trang {start}" if start == end else f"trang {start}–{end}"


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pdf", required=True)
    ap.add_argument("--code", required=True, help="Mã sách, chữ thường không dấu, ví dụ toan6-t1")
    ap.add_argument("--subject", required=True, help="Tên môn đúng như trong hệ thống, ví dụ Toán")
    ap.add_argument("--grade", type=int, required=True)
    ap.add_argument("--volume", type=int, default=1)
    ap.add_argument("--title", required=True)
    ap.add_argument("--toc-pages", required=True, help="Số trang PDF chứa mục lục, cách nhau bởi dấu phẩy. Ví dụ 5 hoặc 5,6")
    ap.add_argument("--offset", type=int, help="Số trang PDF = số in + offset. Bỏ trống để tự dò")
    ap.add_argument("--last-page", type=int, help="Số in của trang cuối phần nội dung (mặc định: trang cuối của sách)")
    ap.add_argument("--zoom", type=float, default=1.0, help="Tỉ lệ ảnh trang, mặc định 1.0")
    ap.add_argument("--quality", type=int, default=80, help="Chất lượng JPG 1-100, mặc định 80")
    ap.add_argument("--no-upload", action="store_true", help="Không tải ảnh lên Cloudinary (chỉ lập mục lục)")
    ap.add_argument("--force", action="store_true", help="Tải lại cả những trang đã có trên Cloudinary")
    a = ap.parse_args()

    key, model = load_ai_settings()
    doc = pymupdf.open(a.pdf)
    print(f"PDF {len(doc)} trang")

    if not a.no_upload:
        cloud, c_key, c_secret = load_cloudinary()
        work = WORK_DIR / a.code
        work.mkdir(parents=True, exist_ok=True)

        def push(i: int) -> str:
            name = page_name(a.code, i + 1)
            if not a.force and cloudinary_exists(cloud, name):
                return "có sẵn"
            data = render_jpg(doc[i], a.zoom, a.quality)
            (work / f"{name}.jpg").write_bytes(data)
            cloudinary_upload(cloud, c_key, c_secret, name, data)
            return "đã tải"

        with ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(push, range(len(doc))))
        print(f"Cloudinary {CLOUD_FOLDER}: tải {results.count('đã tải')} trang, đã có sẵn {results.count('có sẵn')} trang.")

    offset = a.offset if a.offset is not None else detect_offset(doc, key, model)
    print(f"Độ lệch số trang: PDF = số in + {offset}")

    entries = read_toc(doc, key, model, [int(p) for p in a.toc_pages.split(",")])
    last_printed = a.last_page if a.last_page else len(doc) - offset
    lessons = build_lessons(entries, last_printed)

    for l in lessons:
        n = l["endPage"] - l["startPage"] + 1
        flag = f"  <-- dài {n} trang, quá {MAX_LESSON_PAGES}" if n > MAX_LESSON_PAGES else ""
        print(f"  {l['order']:>2}. [{l['chapter'][:28]}] {l['title']}: {pages_text(l['startPage'], l['endPage'])}{flag}")

    book = {
        "code": a.code, "subject": a.subject, "grade": a.grade, "volume": a.volume, "title": a.title,
        "pageOffset": offset, "pdfPageCount": len(doc), "lessons": lessons,
    }
    BOOK_DIR.mkdir(parents=True, exist_ok=True)
    out = BOOK_DIR / f"{a.code}.json"
    out.write_text(json.dumps(book, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Đã ghi {out}. Đối chiếu bảng trên với mục lục trong sách rồi commit file này.")


if __name__ == "__main__":
    main()
