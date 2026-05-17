// swift-tools-version: 6.2
import CompilerPluginSupport
import Foundation
import PackageDescription

let sweetCookieKitPath = "../SweetCookieKit"
let useLocalSweetCookieKit =
    ProcessInfo.processInfo.environment["CODEXBAR_USE_LOCAL_SWEETCOOKIEKIT"] == "1"
#if os(macOS)
let sweetCookieKitDependency: Package.Dependency? =
    useLocalSweetCookieKit && FileManager.default.fileExists(atPath: sweetCookieKitPath)
    ? .package(path: sweetCookieKitPath)
    : .package(url: "https://github.com/steipete/SweetCookieKit", from: "0.4.1")
let sweetCookieKitTargetDependency: Target.Dependency = .product(
    name: "SweetCookieKit",
    package: "SweetCookieKit")
#else
let sweetCookieKitDependency: Package.Dependency? = nil
let sweetCookieKitTargetDependency: Target.Dependency = "SweetCookieKit"
#endif

let sharedPackageDependencies: [Package.Dependency] = [
    .package(url: "https://github.com/steipete/Commander", from: "0.2.1"),
    .package(url: "https://github.com/apple/swift-crypto.git", from: "3.0.0"),
    .package(url: "https://github.com/apple/swift-log", from: "1.12.0"),
    .package(url: "https://github.com/apple/swift-syntax", from: "600.0.1"),
]

let packageDependencies =
    sharedPackageDependencies + (sweetCookieKitDependency.map { [$0] } ?? [])

let package = Package(
    name: "CodexBarWindows",
    defaultLocalization: "en",
    platforms: [
        .macOS(.v14),
    ],
    dependencies: packageDependencies,
    targets: {
        var targets: [Target] = [
            .target(
                name: "CodexBarCore",
                dependencies: [
                    "CodexBarMacroSupport",
                    .product(name: "Crypto", package: "swift-crypto"),
                    .product(name: "Logging", package: "swift-log"),
                    sweetCookieKitTargetDependency,
                ],
                swiftSettings: [
                    .enableUpcomingFeature("StrictConcurrency"),
                ]),
            .macro(
                name: "CodexBarMacros",
                dependencies: [
                    .product(name: "SwiftCompilerPlugin", package: "swift-syntax"),
                    .product(name: "SwiftSyntaxBuilder", package: "swift-syntax"),
                    .product(name: "SwiftSyntaxMacros", package: "swift-syntax"),
                ]),
            .target(
                name: "CodexBarMacroSupport",
                dependencies: [
                    "CodexBarMacros",
                ]),
            .executableTarget(
                name: "CodexBarCLI",
                dependencies: [
                    "CodexBarCore",
                    .product(name: "Commander", package: "Commander"),
                ],
                path: "Sources/CodexBarCLI",
                swiftSettings: [
                    .enableUpcomingFeature("StrictConcurrency"),
                ]),
            .testTarget(
                name: "CodexBarLinuxTests",
                dependencies: ["CodexBarCore", "CodexBarCLI"],
                path: "TestsLinux",
                swiftSettings: [
                    .enableUpcomingFeature("StrictConcurrency"),
                    .enableExperimentalFeature("SwiftTesting"),
                ]),
        ]

        #if !os(macOS)
        targets.append(.target(
            name: "SweetCookieKit",
            path: "Sources/SweetCookieKitWindowsStub",
            swiftSettings: [
                .enableUpcomingFeature("StrictConcurrency"),
            ]))
        #endif

        return targets
    }())
