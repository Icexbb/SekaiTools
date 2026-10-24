import Foundation
import Combine

struct SourceOption: Decodable, Identifiable { let id: Int; let name: String }
struct Capabilities: Decodable {
    let subtitleAvailable: Bool
    let subtitleReason: String
    let sources: [SourceOption]
}
struct TranslationRow: Codable, Identifiable, Equatable {
    let id: Int
    let kind: String
    let originalSpeaker: String
    let original: String
    var speaker: String
    var content: String
}
struct DownloadRow: Decodable, Identifiable {
    let id: String
    let title: String
    let group: String
}
struct TimingRow: Decodable, Identifiable {
    let kind: String
    let index: Int
    let content: String
    let startFrame: Int
    let endFrame: Int
    let start: String
    let end: String
    var id: String { "\(kind)-\(index)" }
}
struct SubtitleResult: Decodable {
    let canceled: Bool
    let stopReason: String
    let summary: String?
    let output: String?
    let rows: [TimingRow]?
}
struct ProgressUpdate: Decodable {
    let stage: String?
    let value: Double?
    let fps: Int?
    let etaSeconds: Double?
    let warning: String?
}
struct HostFailure: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

@MainActor
final class HostClient: ObservableObject {
    @Published var busy = false
    @Published var status = "就绪"
    @Published var progress = 0.0
    @Published var logs = ""
    private var process: Process?
    private var input: Pipe?

    func cancel() {
        guard let input else { return }
        do {
            try input.fileHandleForWriting.write(contentsOf: Data("{\"command\":\"cancel\"}\n".utf8))
            status = "正在停止并保存进度…"
        } catch { status = error.localizedDescription }
    }

    func call<T: Decodable>(_ command: String, args: [String: Any] = [:]) async throws -> T {
        guard !busy else { throw HostFailure(message: "请等待当前操作完成") }
        let host = ProcessInfo.processInfo.environment["SEKAITOOLS_HOST_PATH"]
            ?? Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/SekaiToolsHost").path
        guard FileManager.default.isExecutableFile(atPath: host) else {
            throw HostFailure(message: "找不到本地处理程序，请使用开发构建脚本生成 .app，或设置 SEKAITOOLS_HOST_PATH。")
        }
        let request = try JSONSerialization.data(withJSONObject: ["version": 1, "command": command, "args": args])
        let child = Process()
        let stdin = Pipe(), stdout = Pipe(), stderr = Pipe()
        child.executableURL = URL(fileURLWithPath: host)
        child.standardInput = stdin
        child.standardOutput = stdout
        child.standardError = stderr
        // Inherit developer runtime overrides, including SEKAITOOLS_CVEXTERN_PATH.
        child.environment = ProcessInfo.processInfo.environment
        stderr.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            if data.isEmpty { handle.readabilityHandler = nil; return }
            let message = String(decoding: data, as: UTF8.self)
            Task { @MainActor in
                guard let self else { return }
                self.logs = String((self.logs + message).suffix(32000))
            }
        }
        busy = true; progress = 0; status = "正在处理…"
        process = child; input = stdin
        defer {
            stderr.fileHandleForReading.readabilityHandler = nil
            try? stdin.fileHandleForWriting.close()
            process = nil; input = nil; busy = false
        }
        try child.run()
        try stdin.fileHandleForWriting.write(contentsOf: request + Data([10]))
        var result: T?
        var failure: String?
        for try await line in stdout.fileHandleForReading.bytes.lines {
            guard let bytes = line.data(using: .utf8),
                  let envelope = try JSONSerialization.jsonObject(with: bytes) as? [String: Any],
                  envelope["version"] as? Int == 1,
                  let kind = envelope["type"] as? String,
                  let payload = envelope["data"] else {
                failure = "处理程序返回了无效协议数据"; continue
            }
            let data = try JSONSerialization.data(withJSONObject: payload, options: [.fragmentsAllowed])
            switch kind {
            case "progress":
                let update = try JSONDecoder().decode(ProgressUpdate.self, from: data)
                if let value = update.value { progress = min(1, max(0, value)) }
                if let stage = update.stage { status = stage }
                if let fps = update.fps { status = "识别速度 \(fps) fps，预计剩余 \(Int(update.etaSeconds ?? 0)) 秒" }
                if let warning = update.warning { logs = String((logs + "\n" + warning).suffix(32000)) }
            case "result": result = try JSONDecoder().decode(T.self, from: data)
            case "error": failure = (payload as? [String: Any])?["message"] as? String ?? "操作失败"
            default: failure = "未知协议响应"
            }
        }
        if let failure { status = "失败"; throw HostFailure(message: failure) }
        guard let result else { status = "失败"; throw HostFailure(message: "处理程序退出且未返回结果，请查看运行日志。") }
        status = "操作完成"
        return result
    }
}

struct FileResult: Decodable { let output: String }
