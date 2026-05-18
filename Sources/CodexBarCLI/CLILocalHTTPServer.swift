import Foundation
#if os(Windows)
import WinSDK
#elseif canImport(Darwin)
import Darwin
#elseif canImport(Glibc)
import Glibc
#endif

private let requestReadTimeoutMilliseconds: Int32 = 5000

struct CLILocalHTTPRequest {
    let method: String
    let target: String
    let host: String
    let path: String
    let queryItems: [String: String]

    static func parse(_ data: Data) -> Result<CLILocalHTTPRequest, CLILocalHTTPRequestParseError> {
        guard let raw = String(data: data, encoding: .utf8),
              let firstLine = raw.components(separatedBy: "\r\n").first
        else {
            return .failure(.invalidRequest)
        }

        let parts = firstLine.split(separator: " ")
        guard parts.count >= 3 else { return .failure(.invalidRequest) }

        let method = String(parts[0]).uppercased()
        let target = String(parts[1])
        guard target.hasPrefix("/") else { return .failure(.invalidRequest) }

        let headerResult = Self.parseHeaders(raw)
        let host: String
        switch headerResult {
        case let .success(headers):
            let hosts = headers.compactMap { name, value in
                name.lowercased() == "host" ? value : nil
            }
            guard let candidate = hosts.first else { return .failure(.missingHost) }
            guard hosts.count == 1 else { return .failure(.duplicateHost) }
            guard Self.isAllowedLoopbackHost(candidate) else { return .failure(.disallowedHost) }
            host = candidate
        case let .failure(error):
            return .failure(error)
        }

        let components = URLComponents(string: "http://localhost\(target)")
        let path = components?.path ?? target
        var queryItems: [String: String] = [:]
        for item in components?.queryItems ?? [] {
            if let value = item.value {
                queryItems[item.name] = value
            }
        }

        return .success(CLILocalHTTPRequest(
            method: method,
            target: target,
            host: host,
            path: path,
            queryItems: queryItems))
    }

    private static func parseHeaders(_ raw: String) -> Result<[(String, String)], CLILocalHTTPRequestParseError> {
        let lines = raw.components(separatedBy: "\r\n")
        var headers: [(String, String)] = []

        for line in lines.dropFirst() {
            if line.isEmpty { break }
            guard let separator = line.firstIndex(of: ":") else {
                return .failure(.invalidRequest)
            }

            let name = String(line[..<separator]).trimmingCharacters(in: .whitespacesAndNewlines)
            let value = String(line[line.index(after: separator)...]).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !name.isEmpty else { return .failure(.invalidRequest) }
            headers.append((name, value))
        }

        return .success(headers)
    }

    private static func isAllowedLoopbackHost(_ host: String) -> Bool {
        let trimmed = host.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !trimmed.contains(",") else { return false }

        let hostWithoutPort: String
        if trimmed.hasPrefix("[") {
            guard let closingBracket = trimmed.firstIndex(of: "]") else { return false }
            hostWithoutPort = String(trimmed[...closingBracket])
            let remainder = trimmed[trimmed.index(after: closingBracket)...]
            guard remainder.isEmpty || Self.isValidPortSuffix(String(remainder)) else { return false }
        } else {
            let segments = trimmed.split(separator: ":", omittingEmptySubsequences: false)
            switch segments.count {
            case 1:
                hostWithoutPort = String(segments[0])
            case 2:
                guard Self.isValidPort(String(segments[1])) else { return false }
                hostWithoutPort = String(segments[0])
            default:
                return false
            }
        }

        switch hostWithoutPort.lowercased() {
        case "127.0.0.1", "localhost", "localhost.", "[::1]":
            return true
        default:
            return false
        }
    }

    private static func isValidPortSuffix(_ raw: String) -> Bool {
        guard raw.hasPrefix(":") else { return false }
        return self.isValidPort(String(raw.dropFirst()))
    }

    private static func isValidPort(_ raw: String) -> Bool {
        guard let port = Int(raw), port > 0, port <= Int(UInt16.max) else { return false }
        return true
    }
}

enum CLILocalHTTPRequestParseError: Error, Equatable {
    case invalidRequest
    case missingHost
    case duplicateHost
    case disallowedHost
}

enum CLIHTTPStatus {
    case ok
    case badRequest
    case forbidden
    case notFound
    case methodNotAllowed
    case internalServerError

    var code: Int {
        switch self {
        case .ok: 200
        case .badRequest: 400
        case .forbidden: 403
        case .notFound: 404
        case .methodNotAllowed: 405
        case .internalServerError: 500
        }
    }

    var reason: String {
        switch self {
        case .ok: "OK"
        case .badRequest: "Bad Request"
        case .forbidden: "Forbidden"
        case .notFound: "Not Found"
        case .methodNotAllowed: "Method Not Allowed"
        case .internalServerError: "Internal Server Error"
        }
    }
}

struct CLILocalHTTPResponse {
    let status: CLIHTTPStatus
    let body: Data
    let contentType: String

    init(status: CLIHTTPStatus, body: Data, contentType: String = "application/json; charset=utf-8") {
        self.status = status
        self.body = body
        self.contentType = contentType
    }

    var serialized: Data {
        var headers = "HTTP/1.1 \(self.status.code) \(self.status.reason)\r\n"
        headers += "Content-Type: \(self.contentType)\r\n"
        headers += "Content-Length: \(self.body.count)\r\n"
        headers += "Connection: close\r\n"
        headers += "\r\n"

        var data = Data(headers.utf8)
        data.append(self.body)
        return data
    }
}

enum CLILocalHTTPServerError: LocalizedError {
    case winsockStartupFailed(Int32)
    case addressResolutionFailed(host: String, port: UInt16, code: Int32)
    case socketCreationFailed(Int32)
    case bindFailed(Int32)
    case listenFailed(Int32)

    var errorDescription: String? {
        switch self {
        case let .winsockStartupFailed(code):
            "Windows socket startup failed with code \(code)."
        case let .addressResolutionFailed(host, port, code):
            "Failed to resolve local bind address \(host):\(port) (code \(code))."
        case let .socketCreationFailed(code):
            "Failed to create local HTTP socket (code \(code))."
        case let .bindFailed(code):
            "Failed to bind local HTTP socket (code \(code))."
        case let .listenFailed(code):
            "Failed to listen on local HTTP socket (code \(code))."
        }
    }
}

final class CLILocalHTTPServer {
    typealias Handler = @Sendable (CLILocalHTTPRequest) async -> CLILocalHTTPResponse

    private let host: String
    private let port: UInt16
    private let handler: Handler

    init(host: String, port: UInt16, handler: @escaping Handler) {
        self.host = host
        self.port = port
        self.handler = handler
    }

    func run(onListening: @Sendable () -> Void = {}) async throws {
        #if os(Windows)
        try startWindowsSockets()
        defer { WSACleanup() }

        let serverSocket = try makeWindowsServerSocket(host: self.host, port: self.port)
        defer { closeWindowsSocket(serverSocket) }

        onListening()

        while true {
            var clientAddress = sockaddr_storage()
            var clientLength = Int32(MemoryLayout<sockaddr_storage>.size)
            let clientSocket = withUnsafeMutablePointer(to: &clientAddress) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { socketAddress in
                    accept(serverSocket, socketAddress, &clientLength)
                }
            }
            guard clientSocket != INVALID_SOCKET else { continue }
            setWindowsReceiveTimeout(clientSocket)

            let handler = self.handler
            Task {
                defer { closeWindowsSocket(clientSocket) }
                await handleWindowsClient(clientSocket, handler: handler)
            }
        }
        #else
        ignoreSIGPIPE()

        #if canImport(Darwin)
        let streamType = SOCK_STREAM
        #else
        let streamType = Int32(SOCK_STREAM.rawValue)
        #endif

        let serverFD = socket(AF_INET, streamType, 0)
        guard serverFD >= 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
        defer { closeSocket(serverFD) }

        var reuse: Int32 = 1
        setsockopt(
            serverFD,
            SOL_SOCKET,
            SO_REUSEADDR,
            &reuse,
            socklen_t(MemoryLayout<Int32>.size))

        var address = sockaddr_in()
        #if canImport(Darwin)
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        #endif
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = self.port.bigEndian
        guard inet_pton(AF_INET, self.host, &address.sin_addr) == 1 else {
            throw POSIXError(.EADDRNOTAVAIL)
        }

        let bound = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { socketAddress in
                bind(serverFD, socketAddress, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bound == 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }

        guard listen(serverFD, 16) == 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
        onListening()

        while true {
            var clientAddress = sockaddr()
            var clientLength = socklen_t(MemoryLayout<sockaddr>.size)
            let clientFD = accept(serverFD, &clientAddress, &clientLength)
            guard clientFD >= 0 else { continue }
            let handler = self.handler
            Task {
                defer { closeSocket(clientFD) }
                await handleClient(clientFD, handler: handler)
            }
        }
        #endif
    }
}

#if os(Windows)
private func startWindowsSockets() throws {
    var data = WSADATA()
    let result = WSAStartup(UInt16(0x0202), &data)
    guard result == 0 else {
        throw CLILocalHTTPServerError.winsockStartupFailed(result)
    }
}

private func makeWindowsServerSocket(host: String, port: UInt16) throws -> SOCKET {
    var hints = addrinfo()
    hints.ai_family = AF_INET
    hints.ai_socktype = SOCK_STREAM
    hints.ai_protocol = 0
    hints.ai_flags = AI_PASSIVE

    var results: UnsafeMutablePointer<addrinfo>?
    let status = host.withCString { hostPointer in
        String(port).withCString { portPointer in
            getaddrinfo(hostPointer, portPointer, &hints, &results)
        }
    }

    guard status == 0 else {
        throw CLILocalHTTPServerError.addressResolutionFailed(host: host, port: port, code: status)
    }
    defer {
        if let results {
            freeaddrinfo(results)
        }
    }

    var firstSocketError: Int32?
    var current = results
    while let candidate = current {
        let info = candidate.pointee
        let socketHandle = socket(info.ai_family, info.ai_socktype, info.ai_protocol)
        guard socketHandle != INVALID_SOCKET else {
            firstSocketError = firstSocketError ?? WSAGetLastError()
            current = info.ai_next
            continue
        }

        var reuse: Int32 = 1
        _ = withUnsafePointer(to: &reuse) { pointer in
            pointer.withMemoryRebound(to: CChar.self, capacity: MemoryLayout<Int32>.size) { optionPointer in
                setsockopt(
                    socketHandle,
                    SOL_SOCKET,
                    SO_REUSEADDR,
                    optionPointer,
                    Int32(MemoryLayout<Int32>.size))
            }
        }

        if bind(socketHandle, info.ai_addr, Int32(info.ai_addrlen)) == 0 {
            guard listen(socketHandle, 16) == 0 else {
                let code = WSAGetLastError()
                closeWindowsSocket(socketHandle)
                throw CLILocalHTTPServerError.listenFailed(code)
            }
            return socketHandle
        }

        firstSocketError = firstSocketError ?? WSAGetLastError()
        closeWindowsSocket(socketHandle)
        current = info.ai_next
    }

    if let firstSocketError {
        throw CLILocalHTTPServerError.bindFailed(firstSocketError)
    }
    throw CLILocalHTTPServerError.socketCreationFailed(WSAGetLastError())
}

private func handleWindowsClient(
    _ clientSocket: SOCKET,
    handler: @Sendable (CLILocalHTTPRequest) async -> CLILocalHTTPResponse) async
{
    let request: CLILocalHTTPRequest
    switch readWindowsRequest(clientSocket) {
    case let .success(parsedRequest):
        request = parsedRequest
    case .failure(.disallowedHost):
        sendWindowsResponse(
            CLILocalHTTPResponse(
                status: .forbidden,
                body: Data(#"{"error":"forbidden host"}"#.utf8)),
            to: clientSocket)
        return
    case .failure:
        sendWindowsResponse(
            CLILocalHTTPResponse(
                status: .badRequest,
                body: Data(#"{"error":"invalid request"}"#.utf8)),
            to: clientSocket)
        return
    }

    let response = await handler(request)
    sendWindowsResponse(response, to: clientSocket)
}

private func readWindowsRequest(_ socket: SOCKET) -> Result<CLILocalHTTPRequest, CLILocalHTTPRequestParseError> {
    var data = Data()
    var buffer = [UInt8](repeating: 0, count: 4096)
    var sawHeaderEnd = false

    while data.count < 16384 {
        let count = buffer.withUnsafeMutableBytes { rawBuffer in
            guard let baseAddress = rawBuffer.baseAddress else { return Int32(SOCKET_ERROR) }
            return recv(
                socket,
                baseAddress.assumingMemoryBound(to: CChar.self),
                Int32(buffer.count),
                0)
        }
        guard count > 0 else { break }
        data.append(buffer, count: Int(count))
        if data.range(of: Data("\r\n\r\n".utf8)) != nil {
            sawHeaderEnd = true
            break
        }
    }

    guard sawHeaderEnd else { return .failure(.invalidRequest) }
    return CLILocalHTTPRequest.parse(data)
}

private func sendWindowsResponse(_ response: CLILocalHTTPResponse, to socket: SOCKET) {
    let data = response.serialized
    data.withUnsafeBytes { rawBuffer in
        guard let base = rawBuffer.baseAddress else { return }
        var sent = 0
        while sent < data.count {
            let chunkLength = min(data.count - sent, Int(Int32.max))
            let count = send(
                socket,
                base.advanced(by: sent).assumingMemoryBound(to: CChar.self),
                Int32(chunkLength),
                0)
            guard count > 0 else { break }
            sent += Int(count)
        }
    }
}

private func setWindowsReceiveTimeout(_ socket: SOCKET) {
    var timeout = DWORD(requestReadTimeoutMilliseconds)
    _ = withUnsafePointer(to: &timeout) { pointer in
        pointer.withMemoryRebound(to: CChar.self, capacity: MemoryLayout<DWORD>.size) { optionPointer in
            setsockopt(
                socket,
                SOL_SOCKET,
                SO_RCVTIMEO,
                optionPointer,
                Int32(MemoryLayout<DWORD>.size))
        }
    }
}

private func closeWindowsSocket(_ socket: SOCKET) {
    closesocket(socket)
}
#else
private func handleClient(
    _ clientFD: Int32,
    handler: @Sendable (CLILocalHTTPRequest) async -> CLILocalHTTPResponse) async
{
    let request: CLILocalHTTPRequest
    switch readRequest(clientFD) {
    case let .success(parsedRequest):
        request = parsedRequest
    case .failure(.disallowedHost):
        sendResponse(
            CLILocalHTTPResponse(
                status: .forbidden,
                body: Data(#"{"error":"forbidden host"}"#.utf8)),
            to: clientFD)
        return
    case .failure:
        sendResponse(
            CLILocalHTTPResponse(
                status: .badRequest,
                body: Data(#"{"error":"invalid request"}"#.utf8)),
            to: clientFD)
        return
    }

    let response = await handler(request)
    sendResponse(response, to: clientFD)
}

private func readRequest(_ fd: Int32) -> Result<CLILocalHTTPRequest, CLILocalHTTPRequestParseError> {
    var data = Data()
    var buffer = [UInt8](repeating: 0, count: 4096)
    let bufferSize = buffer.count
    var sawHeaderEnd = false

    while data.count < 16384 {
        guard waitForReadable(fd, timeoutMilliseconds: requestReadTimeoutMilliseconds) else {
            return .failure(.invalidRequest)
        }
        let count = buffer.withUnsafeMutableBytes { rawBuffer in
            recv(fd, rawBuffer.baseAddress, bufferSize, 0)
        }
        guard count > 0 else { break }
        data.append(buffer, count: count)
        if data.range(of: Data("\r\n\r\n".utf8)) != nil {
            sawHeaderEnd = true
            break
        }
    }

    guard sawHeaderEnd else { return .failure(.invalidRequest) }
    return CLILocalHTTPRequest.parse(data)
}

private func sendResponse(_ response: CLILocalHTTPResponse, to fd: Int32) {
    let data = response.serialized
    data.withUnsafeBytes { rawBuffer in
        guard let base = rawBuffer.baseAddress else { return }
        var sent = 0
        while sent < data.count {
            let count = send(fd, base.advanced(by: sent), data.count - sent, sendNoSignalFlags())
            guard count > 0 else { break }
            sent += count
        }
    }
}

private func waitForReadable(_ fd: Int32, timeoutMilliseconds: Int32) -> Bool {
    var pollFD = pollfd(fd: fd, events: Int16(POLLIN), revents: 0)
    while true {
        let result = poll(&pollFD, 1, timeoutMilliseconds)
        if result > 0 {
            return (pollFD.revents & Int16(POLLIN)) != 0
        }
        if result == -1, errno == EINTR {
            continue
        }
        return false
    }
}

private func sendNoSignalFlags() -> Int32 {
    #if canImport(Darwin)
    0
    #else
    Int32(MSG_NOSIGNAL)
    #endif
}

private func ignoreSIGPIPE() {
    #if canImport(Darwin)
    _ = Darwin.signal(SIGPIPE, SIG_IGN)
    #else
    _ = Glibc.signal(SIGPIPE, SIG_IGN)
    #endif
}

private func closeSocket(_ fd: Int32) {
    #if canImport(Darwin)
    Darwin.close(fd)
    #else
    Glibc.close(fd)
    #endif
}
#endif
