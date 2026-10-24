// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SekaiToolsMac",
    platforms: [.macOS("15.0")],
    products: [.executable(name: "SekaiToolsMac", targets: ["SekaiToolsMac"])],
    targets: [.executableTarget(name: "SekaiToolsMac")]
)
