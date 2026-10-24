import SwiftUI
import AVKit
import AppKit

struct SubtitleView: View {
    let capabilities: Capabilities?
    let active: Bool
    @EnvironmentObject var host: HostClient
    @EnvironmentObject var state: AppState
    @State private var video = ""
    @State private var script = ""
    @State private var translation = ""
    @State private var resume = false
    @State private var running = false
    @State private var player: AVPlayer?
    @State private var rows: [TimingRow] = []
    @State private var summary = ""
    @State private var output = ""
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("自动轴机").font(.largeTitle)
                GroupBox("输入素材") {
                    VStack(alignment: .leading, spacing: 10) {
                        pathRow("视频", path: video, icon: "film") {
                            if let path = Files.open("选择视频") {
                                player?.pause(); video = path
                                player = AVPlayer(url: URL(fileURLWithPath: path))
                            }
                        }
                        pathRow("剧本", path: script, icon: "doc.text") {
                            if let path = Files.open("选择剧本") { script = path }
                        }
                        pathRow("翻译（可选）", path: translation, icon: "character.bubble") {
                            if let path = Files.open("选择翻译文本") { translation = path }
                        }
                        if !translation.isEmpty { Button("清除翻译") { translation = "" } }
                        Toggle("恢复相同素材的未完成进度", isOn: $resume)
                    }.padding(8).disabled(host.busy)
                }
                if let player {
                    VideoPlayer(player: player).frame(minHeight: 200, idealHeight: 280)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                }
                if let capabilities, !capabilities.subtitleAvailable {
                    Label(capabilities.subtitleReason, systemImage: "exclamationmark.triangle")
                        .foregroundStyle(.orange).textSelection(.enabled)
                }
                HStack {
                    Button("开始识别并导出 ASS", systemImage: "play.fill") { start() }
                        .buttonStyle(.borderedProminent)
                        .disabled(host.busy || video.isEmpty || script.isEmpty || capabilities?.subtitleAvailable != true)
                    if running { Button("停止并保存进度", systemImage: "stop.fill") { host.cancel() } }
                    Spacer()
                    if !output.isEmpty { Button("在 Finder 显示", systemImage: "folder") { NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: output)]) } }
                }
                if running {
                    ProgressView(value: host.progress)
                    Text("\(Int(host.progress * 100))%").font(.caption).monospacedDigit()
                }
                if !summary.isEmpty { Text(summary).textSelection(.enabled) }
                ForEach(rows) { row in
                    GroupBox {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("\(row.kind) · \(row.start) → \(row.end)").font(.caption).foregroundStyle(.secondary)
                            Text(row.content).textSelection(.enabled)
                            Text("帧 \(row.startFrame) – \(row.endFrame)").font(.caption).monospacedDigit()
                        }.frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
            }.padding(20)
        }
        .onDisappear { player?.pause() }
        .onChange(of: active) { _, isActive in if !isActive { player?.pause() } }
    }
    private func pathRow(_ title: String, path: String, icon: String, action: @escaping () -> Void) -> some View {
        HStack {
            Button(title, systemImage: icon, action: action).frame(width: 160, alignment: .leading)
            Text(path.isEmpty ? "未选择" : path).font(.caption).foregroundStyle(.secondary).lineLimit(1).truncationMode(.middle).help(path)
        }
    }
    private func start() {
        guard let path = Files.save("subtitle.ass") else { return }
        running = true; rows = []; output = ""; summary = ""
        Task {
            defer { running = false }
            do {
                let result: SubtitleResult = try await host.call("subtitle.run", args: [
                    "video": video, "script": script, "translation": translation,
                    "output": path, "resume": resume
                ])
                rows = result.rows ?? []
                output = result.output ?? ""
                summary = result.canceled ? "已停止，进度已保存。选择恢复后可以继续。" : (result.summary ?? result.stopReason)
            } catch { state.error = error.localizedDescription }
        }
    }
}

struct DownloadView: View {
    let sources: [SourceOption]
    @EnvironmentObject var host: HostClient
    @EnvironmentObject var state: AppState
    @State private var source = 0
    @State private var category = "event"
    @State private var search = ""
    @State private var rows: [DownloadRow] = []
    @State private var savedPath = ""
    private var filtered: [DownloadRow] {
        rows.filter { search.isEmpty || "\($0.title) \($0.group) \($0.id)".localizedCaseInsensitiveContains(search) }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("下载剧本").font(.largeTitle)
            HStack {
                Picker("数据源", selection: $source) {
                    ForEach(sources) { Text($0.name).tag($0.id) }
                }
                Picker("分类", selection: $category) {
                    Text("活动").tag("event")
                    Text("卡片").tag("card")
                    Text("主线").tag("unit")
                    Text("特殊").tag("special")
                    Text("区域对话").tag("action")
                }
                Button("刷新目录", systemImage: "arrow.clockwise") { refresh() }.disabled(sources.isEmpty)
            }
            TextField("搜索标题、分组或剧本 ID", text: $search)
            if rows.isEmpty {
                ContentUnavailableView("尚无剧本目录", systemImage: "arrow.down.circle", description: Text("选择数据源和分类后刷新目录。"))
            } else {
                List(filtered) { row in
                    HStack {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(row.title.isEmpty ? row.id : row.title)
                            Text("\(row.group) · \(row.id)").font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button("保存", systemImage: "square.and.arrow.down") { download(row) }
                    }.padding(.vertical, 4)
                }
            }
            if !savedPath.isEmpty {
                HStack {
                    Text("已保存：\(savedPath)").font(.caption).lineLimit(1).truncationMode(.middle)
                    Button("在 Finder 显示") { NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: savedPath)]) }
                }
            }
        }.padding(20).disabled(host.busy)
        .onChange(of: source) { _, _ in rows = []; savedPath = "" }
        .onChange(of: category) { _, _ in rows = []; savedPath = "" }
    }
    private func refresh() {
        Task {
            do { rows = try await host.call("download.catalog", args: ["source": source, "category": category, "refresh": true]) }
            catch { state.error = error.localizedDescription }
        }
    }
    private func download(_ row: DownloadRow) {
        // Keep remote IDs out of directory components and suggested filenames.
        let safeName = row.id.map { $0.isLetter || $0.isNumber || $0 == "_" || $0 == "-" ? $0 : "_" }
        guard let path = Files.save(String(safeName) + ".json") else { return }
        Task {
            do {
                let _: FileResult = try await host.call("download.save", args: [
                    "source": source, "category": category, "id": row.id, "output": path
                ])
                savedPath = path
            } catch { state.error = error.localizedDescription }
        }
    }
}
