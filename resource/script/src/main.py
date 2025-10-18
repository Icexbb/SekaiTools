from res_ls import list_resource_types, list_types_resources,dirPath
from garage import put_file, bucket_name
import os

def threaded_upload(resources, max_threads=4):
    import threading
    from queue import Queue

    def worker():
        while True:
            item = q.get()
            if item is None:
                break
            local_path, remote_path = item
            put_file(bucket_name, local_path, remote_path)
            q.task_done()

    q = Queue()
    threads = []
    for i in range(max_threads):
        t = threading.Thread(target=worker)
        t.start()
        threads.append(t)

    for res in resources:
        local_path = os.path.join(dirPath, res["path"])
        remote_path = res["path"]
        q.put((local_path, remote_path))

    q.join()

    for i in range(max_threads):
        q.put(None)
    for t in threads:
        t.join()

if __name__ == "__main__":
    resource_types = list_resource_types()
    for resource_type in resource_types:
        resources = list_types_resources(resource_type)
        file_list_name = resource_type + ".json"
        file_list_path = os.path.join(dirPath ,file_list_name)

        put_file(bucket_name, file_list_path, file_list_name)
        
        threaded_upload(resources, max_threads=20)

        # for res in resources:
            # put_file(bucket_name, os.path.join(dirPath, res["path"]), res["path"])
        