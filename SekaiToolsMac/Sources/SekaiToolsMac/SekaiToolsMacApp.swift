import SwiftUI
import AppKit
import AVKit

@MainActor
final class AppState: ObservableObject {
    @Published var rows: [TranslationRow] = []
    @Published var savedRows: [TranslationRow] = []
    @Published var script = ""
    @Published var translation = ""
    @Published var error: String?
    var dirty: Bool { rows != savedRows }

    func confirmDiscard() -> Bool {
        guard dirty else { return true }
        let alert = NSAlert()
        alert.messageText = "翻译有未保存的修改"
        alert.informativeText = "请先返回保存，或放弃这些修改。"
        alert.addButton(withTitle: "返回保存")
        alert.addButton(withTitle: "放弃修改")
        return alert.runModal() == .alertSecondButtonReturn
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var state: AppState?
    weak var host: HostClient?
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if host?.busy == true {
            let alert = NSAlert()
            alert.messageText = "请等待操作结束后退出"
            alert.informativeText = "轴机可使用停止按钮保存进度；下载和翻译操作请等待完成。"
            alert.runModal()
            return .terminateCancel
        }
        return state?.confirmDiscard() == false ? .terminateCancel : .terminateNow
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

@main
struct SekaiToolsMacApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var state = AppState()
    @StateObject private var host = HostClient()
    var body: some Scene {
        Window("SekaiTools", id: "main") {
            ContentView()
                .environmentObject(state)
                .environmentObject(host)
                .frame(minWidth: 920, minHeight: 640)
                .onAppear { delegate.state = state; delegate.host = host }
                .background(WindowCloseGuard(state: state, host: host))
        }
        .windowStyle(.automatic)
        .commands { CommandGroup(replacing: .newItem) {} }
    }
}

struct WindowCloseGuard: NSViewRepresentable {
    let state: AppState
    let host: HostClient
    func makeCoordinator() -> Coordinator { Coordinator(state: state, host: host) }
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async { view.window?.delegate = context.coordinator }
        return view
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
    @MainActor final class Coordinator: NSObject, NSWindowDelegate {
        let state: AppState
        let host: HostClient
        init(state: AppState, host: HostClient) { self.state = state; self.host = host }
        func windowShouldClose(_ sender: NSWindow) -> Bool {
            if host.busy {
                let alert = NSAlert()
                alert.messageText = "请等待操作结束后关闭窗口"
                alert.informativeText = "轴机可使用停止按钮保存进度。"
                alert.runModal()
                return false
            }
            guard state.confirmDiscard() else { return false }
            // Closing the last window also requests app termination; do not prompt twice.
            state.savedRows = state.rows
            return true
        }
    }
}

@MainActor
enum Files {
    static func open(_ title: String, directory: Bool = false) -> String? {
        let panel = NSOpenPanel()
        panel.title = title; panel.canChooseDirectories = directory
        panel.canChooseFiles = !directory; panel.allowsMultipleSelection = false
        return panel.runModal() == .OK ? panel.url?.path : nil
    }
    static func save(_ name: String) -> String? {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = name
        return panel.runModal() == .OK ? panel.url?.path : nil
    }
}

struct ContentView: View {
    @EnvironmentObject var state: AppState
    @EnvironmentObject var host: HostClient
    @State private var page: String? = "subtitle"
    @State private var capabilities: Capabilities?
    @State private var showLogs = false
    var body: some View {
        NavigationSplitView {
            List(selection: $page) {
                Label("自动轴机", systemImage: "waveform.path").tag("subtitle")
                Label("翻译", systemImage: "character.bubble").tag("translation")
                Label("下载", systemImage: "arrow.down.circle").tag("download")
            }
            .navigationTitle("SekaiTools")
            .navigationSplitViewColumnWidth(min: 170, ideal: 190)
            .disabled(host.busy)
        } detail: {
            VStack(spacing: 0) {
                ZStack {
                    SubtitleView(capabilities: capabilities, active: page == "subtitle").opacity(page == "subtitle" ? 1 : 0)
                        .allowsHitTesting(page == "subtitle").accessibilityHidden(page != "subtitle")
                    TranslationView().opacity(page == "translation" ? 1 : 0)
                        .allowsHitTesting(page == "translation").accessibilityHidden(page != "translation")
                    DownloadView(sources: capabilities?.sources ?? []).opacity(page == "download" ? 1 : 0)
                        .allowsHitTesting(page == "download").accessibilityHidden(page != "download")
                }
                Divider()
                HStack {
                    if host.busy { ProgressView().controlSize(.small) }
                    Text(host.status).font(.caption).lineLimit(1)
                    Spacer()
                    Button("运行日志", systemImage: "text.alignleft") { showLogs = true }
                }.padding(10)
            }
        }
        .task { await connect() }
        .toolbar { Button("检查环境", systemImage: "arrow.clockwise") { Task { await connect() } }.disabled(host.busy) }
        .alert("操作失败", isPresented: Binding(get: { state.error != nil }, set: { if !$0 { state.error = nil } })) {
            Button("好", role: .cancel) { state.error = nil }
        } message: { Text(state.error ?? "") }
        .sheet(isPresented: $showLogs) {
            VStack(alignment: .leading) {
                Text("运行日志").font(.headline)
                ScrollView { Text(host.logs.isEmpty ? "暂无日志" : host.logs).font(.system(.caption, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                Button("关闭") { showLogs = false }
            }.padding().frame(width: 720, height: 460)
        }
    }
    private func connect() async {
        do { capabilities = try await host.call("capabilities") }
        catch { state.error = error.localizedDescription }
    }
}

struct TranslationView: View {
    @EnvironmentObject var state: AppState
    @EnvironmentObject var host: HostClient
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("翻译").font(.largeTitle)
                if state.dirty { Text("未保存").foregroundStyle(.orange) }
                Spacer()
                Button("载入剧本", systemImage: "doc") { loadScript() }
                Button("导入翻译", systemImage: "square.and.arrow.down") { importTranslation() }.disabled(state.script.isEmpty)
                Button("保存 v2", systemImage: "square.and.arrow.up") { save() }.disabled(state.rows.isEmpty)
            }
            Text(state.script.isEmpty ? "载入 JSON / asset 剧本，编辑角色和正文后保存为翻译文本。" : state.script)
                .font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
            if state.rows.isEmpty {
                ContentUnavailableView("尚未载入剧本", systemImage: "character.bubble", description: Text("可以先到下载页面获取剧本。"))
            } else {
                ScrollView {
                    LazyVStack(spacing: 12) {
                        ForEach($state.rows) { $row in
                            GroupBox {
                                VStack(alignment: .leading, spacing: 8) {
                                    Text("\(row.id + 1) · \(row.kind) · \(row.originalSpeaker)").font(.caption).foregroundStyle(.secondary)
                                    Text(row.original).textSelection(.enabled)
                                    if row.kind == "Dialog" { TextField("角色译名", text: $row.speaker) }
                                    TextField("译文", text: $row.content, axis: .vertical).lineLimit(2...12)
                                }.frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                    }
                }
            }
        }.padding(20).disabled(host.busy)
    }
    private func loadScript() {
        guard state.confirmDiscard(), let path = Files.open("选择剧本 JSON / asset") else { return }
        Task {
            do {
                let rows: [TranslationRow] = try await host.call("translation.load", args: ["script": path])
                state.script = path; state.translation = ""; state.rows = rows; state.savedRows = rows
            } catch { state.error = error.localizedDescription }
        }
    }
    private func importTranslation() {
        guard state.confirmDiscard(), let path = Files.open("选择翻译文本") else { return }
        Task {
            do {
                let rows: [TranslationRow] = try await host.call("translation.load", args: ["script": state.script, "translation": path])
                state.translation = path; state.rows = rows; state.savedRows = rows
            } catch { state.error = error.localizedDescription }
        }
    }
    private func save() {
        guard let path = Files.save("translation.txt") else { return }
        Task {
            do {
                let rows = state.rows
                let json = try JSONSerialization.jsonObject(with: JSONEncoder().encode(rows))
                let _: FileResult = try await host.call("translation.save", args: ["script": state.script, "output": path, "rows": json])
                state.savedRows = rows; state.translation = path
            } catch { state.error = error.localizedDescription }
        }
    }
}
