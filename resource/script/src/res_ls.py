import os
import hashlib
import json

dirPath = os.path.realpath(os.path.join(
    os.path.dirname(__file__), '..', '..', "data"))


def get_file_md5(filePath):
    hash_md5 = hashlib.md5()
    with open(filePath, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            hash_md5.update(chunk)
    return hash_md5.hexdigest()


def list_resource_types():
    resource_types = []
    for entry in os.scandir(dirPath):
        if entry.is_dir() and not entry.name.startswith('.'):
            resource_types.append(entry.name)
    return resource_types


def list_types_resources(resource_type):
    resources = []
    type_dir = os.path.join(dirPath, resource_type)
    if not os.path.isdir(type_dir):
        return resources
    for root, dirs, files in os.walk(type_dir):
        for file in files:
            filePath = os.path.join(root, file)
            relativePath = os.path.relpath(
                filePath, dirPath).replace("\\", "/")
            fileSize = os.path.getsize(filePath)
            fileMD5 = get_file_md5(filePath)
            resources.append({
                "path": relativePath,
                "size": fileSize,
                "md5": fileMD5
            })
    json.dump(resources, open(os.path.join(
        dirPath, f"{resource_type}.json"), "w"), indent=4)

    return resources
