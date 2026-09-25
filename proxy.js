// ap-proxy.js
//
// Local WebSocket relay for old Unity/Mono clients.
//
// Game:
//   ws://127.0.0.1:<localPort>
//
// Proxy:
//   wss://<Archipelago server>:38281
//
// Usage:
//   node ap-proxy.js 38281 wss://archipelago.gg:38281

const WebSocket = require("ws")

const localPort = Number(process.argv[2])
const remoteUrl = process.argv[3]

if (!localPort || !remoteUrl) {
  console.error("Usage: node ap-proxy.js <localPort> <remoteWssUrl>")
  console.error("Example:")
  console.error("  node ap-proxy.js 38281 wss://archipelago.gg:38281")
  process.exit(1)
}

const server = new WebSocket.Server({
  host: "127.0.0.1",
  port: localPort,
})

server.on("listening", () => {
  console.log(`Listening on ws://127.0.0.1:${localPort}`)
  console.log(`Relaying to ${remoteUrl}`)
})

server.on("error", (err) => {
  console.error("[Proxy] Server error:", err)
})

server.on("connection", (clientSocket, request) => {
  console.log("[Proxy] Game client connected.")

  let upstreamOpen = false
  let closing = false

  const pending = []

  const upstream = new WebSocket(remoteUrl, {
    perMessageDeflate: false,
  })

  function closeBoth(code, reason) {
    if (closing) return

    closing = true

    console.log(
      `[Proxy] Closing connection. code=${code} reason=${reason || ""}`,
    )

    // Do not send invalid WebSocket close codes.
    const validCode =
      code >= 1000 &&
      code <= 1015 &&
      code !== 1004 &&
      code !== 1005 &&
      code !== 1006

    const closeCode = validCode ? code : 1000

    if (clientSocket.readyState === WebSocket.OPEN) {
      try {
        clientSocket.close(closeCode, reason || "")
      } catch (e) {
        console.error("[Proxy] Error closing client:", e.message)
      }
    } else if (clientSocket.readyState !== WebSocket.CLOSED) {
      clientSocket.terminate()
    }

    if (upstream.readyState === WebSocket.OPEN) {
      try {
        upstream.close(closeCode, reason || "")
      } catch (e) {
        console.error("[Proxy] Error closing upstream:", e.message)
      }
    } else if (
      upstream.readyState !== WebSocket.CLOSED &&
      upstream.readyState !== WebSocket.CLOSING
    ) {
      upstream.terminate()
    }
  }

  upstream.on("open", () => {
    console.log("[Proxy] Upstream connection established.")

    upstreamOpen = true

    while (pending.length > 0) {
      const frame = pending.shift()

      if (upstream.readyState !== WebSocket.OPEN) break

      try {
        upstream.send(frame.data, {
          binary: frame.isBinary,
        })
      } catch (e) {
        console.error(
          "[Proxy] Failed to flush queued frame:",
          e.message,
        )
        closeBoth(1011, "Proxy send failure")
        return
      }
    }
  })

  upstream.on("message", (data, isBinary) => {
    if (clientSocket.readyState !== WebSocket.OPEN) return

    try {
      clientSocket.send(data, {
        binary: isBinary,
      })
    } catch (e) {
      console.error("[Proxy] Failed sending to game:", e.message)
      closeBoth(1011, "Proxy send failure")
    }
  })

  upstream.on("close", (code, reason) => {
    upstreamOpen = false

    const reasonText = reason ? reason.toString() : ""

    console.log(
      `[Proxy] Upstream closed. Code=${code} Reason=${reasonText}`,
    )

    if (!closing) {
      closeBoth(code, reasonText)
    }
  })

  upstream.on("error", (err) => {
    console.error("[Proxy] Upstream error:", err.message)

    // The close event normally follows this.
    // Do not independently tear everything down twice.
  })

  clientSocket.on("message", (data, isBinary) => {
    if (closing) return

    if (upstreamOpen && upstream.readyState === WebSocket.OPEN) {
      try {
        upstream.send(data, {
          binary: isBinary,
        })
      } catch (e) {
        console.error(
          "[Proxy] Failed sending to upstream:",
          e.message,
        )
        closeBoth(1011, "Proxy send failure")
      }

      return
    }

    // Upstream TLS/WebSocket handshake has not completed yet.
    pending.push({
      data: data,
      isBinary: isBinary,
    })
  })

  clientSocket.on("close", (code, reason) => {
    const reasonText = reason ? reason.toString() : ""

    console.log(
      `[Proxy] Game client disconnected. Code=${code} Reason=${reasonText}`,
    )

    if (!closing) {
      closing = true
      upstreamOpen = false

      if (
        upstream.readyState !== WebSocket.CLOSED &&
        upstream.readyState !== WebSocket.CLOSING
      ) {
        upstream.close(1000, "Game client disconnected")
      }
    }
  })

  clientSocket.on("error", (err) => {
    console.error("[Proxy] Game client socket error:", err.message)
  })
})
