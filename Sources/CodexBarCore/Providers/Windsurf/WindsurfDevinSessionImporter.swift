import Foundation
#if os(macOS)
import SweetCookieKit
#endif

#if os(macOS)
enum WindsurfDevinSessionImporter {
    nonisolated(unsafe) static var importSessionsOverrideForTesting:
        ((BrowserDetection, ((String) -> Void)?) -> [SessionInfo])?
    nonisolated(unsafe) static var importPreferredSessionsOverrideForTesting:
        ((BrowserDetection, ((String) -> Void)?) -> [SessionInfo])?
    nonisolated(unsafe) static var importFallbackSessionsOverrideForTesting:
        ((BrowserDetection, ((String) -> Void)?) -> [SessionInfo])?
    static let defaultPreferredBrowsers: [Browser] = [.chrome]
    static let fallbackBrowsers: [Browser] = [
        .chromeBeta,
        .chromeCanary,
        .edge,
        .edgeBeta,
        .edgeCanary,
        .brave,
        .braveBeta,
        .braveNightly,
        .vivaldi,
        .arc,
        .arcBeta,
        .arcCanary,
        .dia,
        .chatgptAtlas,
        .chromium,
        .helium,
    ]

    struct SessionInfo: Equatable {
        let session: WindsurfDevinSessionAuth
        let sourceLabel: String
    }

    static func importSessions(
        browserDetection: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        if let override = self.importSessionsOverrideForTesting {
            return override(browserDetection, logger)
        }

        let log: (String) -> Void = { msg in logger?("[windsurf-storage] \(msg)") }
        let preferredSessions = self.importSessions(
            browserDetection: browserDetection,
            browsers: self.defaultPreferredBrowsers,
            logger: log)
        if !preferredSessions.isEmpty {
            return preferredSessions
        }

        log("No Windsurf devin session found in Chrome; trying fallback Chromium browsers")
        let sessions = self.importSessions(
            browserDetection: browserDetection,
            browsers: self.fallbackBrowsersExcluding(self.defaultPreferredBrowsers),
            logger: log)

        if sessions.isEmpty {
            log("No Windsurf devin session found in browser local storage")
        }

        return sessions
    }

    static func importPreferredSessions(
        browserDetection: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        if let override = self.importPreferredSessionsOverrideForTesting {
            return override(browserDetection, logger)
        }
        let log: (String) -> Void = { msg in logger?("[windsurf-storage] \(msg)") }
        return self.importSessions(
            browserDetection: browserDetection,
            browsers: self.defaultPreferredBrowsers,
            logger: log)
    }

    static func importFallbackSessions(
        browserDetection: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        if let override = self.importFallbackSessionsOverrideForTesting {
            return override(browserDetection, logger)
        }
        let log: (String) -> Void = { msg in logger?("[windsurf-storage] \(msg)") }
        return self.importSessions(
            browserDetection: browserDetection,
            browsers: self.fallbackBrowsersExcluding(self.defaultPreferredBrowsers),
            logger: log)
    }

    static func fallbackBrowsersExcluding(_ preferredBrowsers: [Browser]) -> [Browser] {
        let preferred = Set(preferredBrowsers)
        return self.fallbackBrowsers.filter { !preferred.contains($0) }
    }

    static func deduplicateSessions(_ sessions: [SessionInfo]) -> [SessionInfo] {
        var deduplicated: [SessionInfo] = []
        var seenSessionTokens = Set<String>()

        for session in sessions {
            guard seenSessionTokens.insert(session.session.sessionToken).inserted else { continue }
            deduplicated.append(session)
        }

        return deduplicated
    }

    static func session(from storage: [String: String], sourceLabel: String) -> SessionInfo? {
        guard let sessionToken = storage["devin_session_token"],
              let auth1Token = storage["devin_auth1_token"],
              let accountID = storage["devin_account_id"],
              let primaryOrgID = storage["devin_primary_org_id"]
        else {
            return nil
        }

        return SessionInfo(
            session: WindsurfDevinSessionAuth(
                sessionToken: sessionToken,
                auth1Token: auth1Token,
                accountID: accountID,
                primaryOrgID: primaryOrgID),
            sourceLabel: sourceLabel)
    }

    static func decodedStorageValue(_ value: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return trimmed }

        if let data = trimmed.data(using: .utf8),
           let decoded = try? JSONDecoder().decode(String.self, from: data)
        {
            return decoded.trimmingCharacters(in: .whitespacesAndNewlines)
        }

        return trimmed.trimmingCharacters(in: CharacterSet(charactersIn: "\""))
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    struct LocalStorageCandidate {
        let label: String
        let url: URL
    }

    private static func importSessions(
        browserDetection: BrowserDetection,
        browsers: [Browser],
        logger: @escaping (String) -> Void) -> [SessionInfo]
    {
        var sessions: [SessionInfo] = []
        let candidates = self.chromeLocalStorageCandidates(
            browserDetection: browserDetection,
            browsers: browsers)
        if !candidates.isEmpty {
            logger("Chrome local storage candidates: \(candidates.count)")
        }

        for candidate in candidates {
            let storage = self.readLocalStorage(from: candidate.url, logger: logger)
            guard let session = self.session(from: storage, sourceLabel: candidate.label) else { continue }
            logger("Found Windsurf devin session in \(candidate.label)")
            sessions.append(session)
        }

        return self.deduplicateSessions(sessions)
    }

    static func chromeLocalStorageCandidates(
        browserDetection: BrowserDetection,
        browsers: [Browser]) -> [LocalStorageCandidate]
    {
        let installedBrowsers = browsers.browsersWithProfileData(using: browserDetection)
        let roots = ChromiumProfileLocator
            .roots(for: installedBrowsers, homeDirectories: BrowserCookieClient.defaultHomeDirectories())
            .map { (url: $0.url, labelPrefix: $0.labelPrefix) }

        var candidates: [LocalStorageCandidate] = []
        for root in roots {
            candidates.append(contentsOf: self.chromeProfileLocalStorageDirs(
                root: root.url,
                labelPrefix: root.labelPrefix))
        }
        return candidates
    }

    private static func chromeProfileLocalStorageDirs(root: URL, labelPrefix: String) -> [LocalStorageCandidate] {
        guard let entries = try? FileManager.default.contentsOfDirectory(
            at: root,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles])
        else { return [] }

        let profileDirs = entries.filter { url in
            guard let isDir = (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory), isDir else {
                return false
            }
            let name = url.lastPathComponent
            return name == "Default" || name.hasPrefix("Profile ") || name.hasPrefix("user-")
        }
        .sorted { $0.lastPathComponent < $1.lastPathComponent }

        return profileDirs.compactMap { dir in
            let levelDBURL = dir.appendingPathComponent("Local Storage").appendingPathComponent("leveldb")
            guard FileManager.default.fileExists(atPath: levelDBURL.path) else { return nil }
            let label = "\(labelPrefix) \(dir.lastPathComponent)"
            return LocalStorageCandidate(label: label, url: levelDBURL)
        }
    }

    private static func readLocalStorage(
        from levelDBURL: URL,
        logger: ((String) -> Void)? = nil) -> [String: String]
    {
        var storage: [String: String] = [:]

        let entries = SweetCookieKit.ChromiumLocalStorageReader.readEntries(
            for: "https://windsurf.com",
            in: levelDBURL,
            logger: logger)

        for entry in entries where Self.targetKeys.contains(entry.key) {
            storage[entry.key] = self.decodedStorageValue(entry.value)
        }

        if storage.count == Self.targetKeys.count {
            return storage
        }

        let textEntries = SweetCookieKit.ChromiumLocalStorageReader.readTextEntries(
            in: levelDBURL,
            logger: logger)

        for entry in textEntries {
            guard storage[entry.key] == nil, Self.targetKeys.contains(entry.key) else { continue }
            storage[entry.key] = self.decodedStorageValue(entry.value)
        }

        return storage
    }

    private static let targetKeys: Set<String> = [
        "devin_session_token",
        "devin_auth1_token",
        "devin_account_id",
        "devin_primary_org_id",
    ]
}
#elseif os(Windows)
enum WindsurfDevinSessionImporter {
    private struct WindowsChromiumBrowser: Equatable {
        let label: String
        let root: URL
    }

    struct SessionInfo: Equatable {
        let session: WindsurfDevinSessionAuth
        let sourceLabel: String
    }

    static func importSessions(
        browserDetection: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        let preferred = self.importPreferredSessions(browserDetection: browserDetection, logger: logger)
        if !preferred.isEmpty {
            return preferred
        }
        return self.importFallbackSessions(browserDetection: browserDetection, logger: logger)
    }

    static func importPreferredSessions(
        browserDetection _: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        self.importSessions(browsers: self.preferredBrowsers(), logger: logger)
    }

    static func importFallbackSessions(
        browserDetection _: BrowserDetection,
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        self.importSessions(browsers: self.fallbackBrowsers(), logger: logger)
    }

    static func deduplicateSessions(_ sessions: [SessionInfo]) -> [SessionInfo] {
        var deduplicated: [SessionInfo] = []
        var seenSessionTokens = Set<String>()

        for session in sessions {
            guard seenSessionTokens.insert(session.session.sessionToken).inserted else { continue }
            deduplicated.append(session)
        }

        return deduplicated
    }

    static func session(from storage: [String: String], sourceLabel: String) -> SessionInfo? {
        guard let sessionToken = storage["devin_session_token"],
              let auth1Token = storage["devin_auth1_token"],
              let accountID = storage["devin_account_id"],
              let primaryOrgID = storage["devin_primary_org_id"]
        else {
            return nil
        }

        return SessionInfo(
            session: WindsurfDevinSessionAuth(
                sessionToken: sessionToken,
                auth1Token: auth1Token,
                accountID: accountID,
                primaryOrgID: primaryOrgID),
            sourceLabel: sourceLabel)
    }

    static func decodedStorageValue(_ value: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return trimmed }

        if let data = trimmed.data(using: .utf8),
           let decoded = try? JSONDecoder().decode(String.self, from: data)
        {
            return decoded.trimmingCharacters(in: .whitespacesAndNewlines)
        }

        return trimmed.trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private struct LocalStorageCandidate {
        let label: String
        let url: URL
    }

    private static func importSessions(
        browsers: [WindowsChromiumBrowser],
        logger: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        let log: (String) -> Void = { msg in logger?("[windsurf-storage] \(msg)") }
        var sessions: [SessionInfo] = []
        for browser in browsers {
            let candidates = self.localStorageCandidates(browser: browser)
            if !candidates.isEmpty {
                log("\(browser.label) local storage candidates: \(candidates.count)")
            }

            for candidate in candidates {
                let storage = self.readLocalStorage(from: candidate.url, logger: log)
                guard let session = self.session(from: storage, sourceLabel: candidate.label) else { continue }
                log("Found Windsurf devin session in \(candidate.label)")
                sessions.append(session)
            }
        }

        if sessions.isEmpty {
            log("No Windsurf devin session found in Windows Chromium local storage")
        }
        return self.deduplicateSessions(sessions)
    }

    private static func preferredBrowsers() -> [WindowsChromiumBrowser] {
        guard let localAppData = self.localAppData else { return [] }
        return [
            WindowsChromiumBrowser(
                label: "Chrome",
                root: localAppData
                    .appendingPathComponent("Google")
                    .appendingPathComponent("Chrome")
                    .appendingPathComponent("User Data")),
        ]
    }

    private static func fallbackBrowsers() -> [WindowsChromiumBrowser] {
        var browsers: [WindowsChromiumBrowser] = []
        if let localAppData = self.localAppData {
            browsers.append(contentsOf: [
                WindowsChromiumBrowser(
                    label: "Edge",
                    root: localAppData
                        .appendingPathComponent("Microsoft")
                        .appendingPathComponent("Edge")
                        .appendingPathComponent("User Data")),
                WindowsChromiumBrowser(
                    label: "Chrome Canary",
                    root: localAppData
                        .appendingPathComponent("Google")
                        .appendingPathComponent("Chrome SxS")
                        .appendingPathComponent("User Data")),
                WindowsChromiumBrowser(
                    label: "Brave",
                    root: localAppData
                        .appendingPathComponent("BraveSoftware")
                        .appendingPathComponent("Brave-Browser")
                        .appendingPathComponent("User Data")),
                WindowsChromiumBrowser(
                    label: "Vivaldi",
                    root: localAppData
                        .appendingPathComponent("Vivaldi")
                        .appendingPathComponent("User Data")),
                WindowsChromiumBrowser(
                    label: "Chromium",
                    root: localAppData
                        .appendingPathComponent("Chromium")
                        .appendingPathComponent("User Data")),
            ])
        }

        if let appData = self.appData {
            browsers.append(contentsOf: [
                WindowsChromiumBrowser(
                    label: "Opera",
                    root: appData
                        .appendingPathComponent("Opera Software")
                        .appendingPathComponent("Opera Stable")),
                WindowsChromiumBrowser(
                    label: "Opera GX",
                    root: appData
                        .appendingPathComponent("Opera Software")
                        .appendingPathComponent("Opera GX Stable")),
            ])
        }
        return browsers
    }

    private static func localStorageCandidates(browser: WindowsChromiumBrowser) -> [LocalStorageCandidate] {
        guard FileManager.default.fileExists(atPath: browser.root.path) else { return [] }
        var candidates: [LocalStorageCandidate] = []

        if let levelDBURL = self.localStorageLevelDBURL(profileRoot: browser.root) {
            candidates.append(LocalStorageCandidate(label: "\(browser.label) Default", url: levelDBURL))
        }

        guard let entries = try? FileManager.default.contentsOfDirectory(
            at: browser.root,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles])
        else {
            return candidates
        }

        let profileDirs = entries.filter { url in
            guard let isDir = (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory), isDir else {
                return false
            }
            let name = url.lastPathComponent
            return name == "Default" || name == "Guest Profile" || name.hasPrefix("Profile ")
        }
        .sorted { $0.lastPathComponent < $1.lastPathComponent }

        var seen = Set<String>(candidates.map(\.url.path))
        for profile in profileDirs {
            guard let levelDBURL = self.localStorageLevelDBURL(profileRoot: profile),
                  seen.insert(levelDBURL.path).inserted
            else {
                continue
            }
            let profileName = profile.lastPathComponent == "Default" ? "Default" : profile.lastPathComponent
            candidates.append(LocalStorageCandidate(label: "\(browser.label) \(profileName)", url: levelDBURL))
        }

        return candidates
    }

    private static func localStorageLevelDBURL(profileRoot: URL) -> URL? {
        let levelDBURL = profileRoot
            .appendingPathComponent("Local Storage")
            .appendingPathComponent("leveldb")
        return FileManager.default.fileExists(atPath: levelDBURL.path) ? levelDBURL : nil
    }

    private static func readLocalStorage(
        from levelDBURL: URL,
        logger: ((String) -> Void)? = nil) -> [String: String]
    {
        guard let files = try? FileManager.default.contentsOfDirectory(
            at: levelDBURL,
            includingPropertiesForKeys: [.contentModificationDateKey],
            options: [.skipsHiddenFiles])
        else {
            return [:]
        }

        let storageFiles = files.filter { url in
            let ext = url.pathExtension.lowercased()
            return ext == "ldb" || ext == "log" || ext == "sst"
        }
        .sorted { lhs, rhs in
            let left = (try? lhs.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate)
            let right = (try? rhs.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate)
            return (left ?? .distantPast) > (right ?? .distantPast)
        }

        var storage: [String: String] = [:]
        for file in storageFiles {
            guard let data = try? Data(contentsOf: file, options: [.mappedIfSafe]) else { continue }
            guard let text = String(data: data, encoding: .utf8) ??
                String(data: data, encoding: .isoLatin1)
            else {
                continue
            }

            for key in self.targetKeys where storage[key] == nil {
                if let value = self.extractStorageValue(for: key, in: text) {
                    storage[key] = value
                }
            }

            if storage.count == self.targetKeys.count {
                return storage
            }
        }

        if !storage.isEmpty, storage.count < self.targetKeys.count {
            logger?("Found partial Windsurf local storage session with \(storage.count)/\(self.targetKeys.count) keys")
        }
        return storage
    }

    private static func extractStorageValue(for key: String, in text: String) -> String? {
        var matches: [String] = []
        var searchRange = text.startIndex..<text.endIndex

        while let range = text.range(of: key, options: [], range: searchRange) {
            let tailStart = range.upperBound
            let distance = text.distance(from: tailStart, to: text.endIndex)
            let tailEnd = text.index(tailStart, offsetBy: min(distance, 2048))
            let tail = String(text[tailStart..<tailEnd])

            if let quoted = self.firstRegexMatch(
                in: tail,
                pattern: #"["']((?:\\.|[^"'\\]){4,})["']"#,
                includeWholeMatch: true)
            {
                let value = self.decodedStorageValue(quoted)
                if self.isLikelyStorageValue(value, key: key) {
                    matches.append(value)
                }
            } else if let token = self.firstRegexMatch(
                in: tail,
                pattern: #"[A-Za-z0-9][A-Za-z0-9._~+/=:-]{7,}"#,
                includeWholeMatch: true),
                self.isLikelyStorageValue(token, key: key)
            {
                matches.append(self.decodedStorageValue(token))
            }

            searchRange = range.upperBound..<text.endIndex
        }

        return matches.last
    }

    private static func firstRegexMatch(
        in text: String,
        pattern: String,
        includeWholeMatch: Bool) -> String?
    {
        guard let regex = try? NSRegularExpression(pattern: pattern, options: []) else { return nil }
        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, options: [], range: range) else { return nil }
        let matchRange = includeWholeMatch || match.numberOfRanges < 2 ? match.range(at: 0) : match.range(at: 1)
        guard let stringRange = Range(matchRange, in: text) else { return nil }
        return String(text[stringRange])
    }

    private static func isLikelyStorageValue(_ value: String, key: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.count >= 4 else { return false }
        guard !trimmed.contains("windsurf.com"),
              !trimmed.contains("Local Storage"),
              trimmed != key
        else {
            return false
        }
        return true
    }

    private static let targetKeys: Set<String> = [
        "devin_session_token",
        "devin_auth1_token",
        "devin_account_id",
        "devin_primary_org_id",
    ]

    private static var localAppData: URL? {
        self.environmentDirectory("LOCALAPPDATA") ??
            self.userProfile?.appendingPathComponent("AppData").appendingPathComponent("Local")
    }

    private static var appData: URL? {
        self.environmentDirectory("APPDATA") ??
            self.userProfile?.appendingPathComponent("AppData").appendingPathComponent("Roaming")
    }

    private static var userProfile: URL? {
        self.environmentDirectory("USERPROFILE")
    }

    private static func environmentDirectory(_ key: String) -> URL? {
        guard let value = ProcessInfo.processInfo.environment[key],
              !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            return nil
        }
        return URL(fileURLWithPath: value)
    }
}
#else
enum WindsurfDevinSessionImporter {
    struct SessionInfo: Equatable {
        let session: WindsurfDevinSessionAuth
        let sourceLabel: String
    }

    static func importSessions(
        browserDetection _: BrowserDetection,
        logger _: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        []
    }

    static func importPreferredSessions(
        browserDetection _: BrowserDetection,
        logger _: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        []
    }

    static func importFallbackSessions(
        browserDetection _: BrowserDetection,
        logger _: ((String) -> Void)? = nil) -> [SessionInfo]
    {
        []
    }
}
#endif
