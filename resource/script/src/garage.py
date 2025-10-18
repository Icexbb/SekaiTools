import os
from dotenv import load_dotenv
import minio

env_path = os.path.join(os.path.dirname(__file__), '..', '.env')
load_dotenv(dotenv_path=env_path, verbose=True)
CONFIG = {
    "ACCESS_KEY": os.getenv("GARAGE_ACCESS_KEY"),
    "SECRET_KEY": os.getenv("GARAGE_SECRET_KEY"),
    "ENDPOINT_URL": os.getenv("GARAGE_ENDPOINT_URL"),
}

bucket_name = os.getenv("GARAGE_BUCKET_NAME")
client = minio.Minio(
    endpoint=CONFIG["ENDPOINT_URL"] or "",
    access_key=CONFIG["ACCESS_KEY"],
    secret_key=CONFIG["SECRET_KEY"],
    secure=False,
    region="garage"
)


def list_objects(bucket_name):
    objects = client.list_objects(bucket_name, recursive=True)
    for obj in objects:
        print(obj.object_name, obj.size, obj.last_modified)


def put_file(bucket_name, local_path, remote_path):
    if not os.path.exists(local_path):
        print(f"File {local_path} does not exist.")
        return
    print(f"Uploading {local_path} to {bucket_name}/{remote_path}...")
    client.fput_object(bucket_name, remote_path, local_path)
