// PiPlay compact-player shell logic (spec 10.3).
//
// Bridges the YouTube IFrame API player to the PiPlay host over WebView2 messaging, following the
// PlayerShellProtocol contract (mirrored in src/PiPlay/Services/PlayerShellProtocol.cs):
//   shell -> host : ready | state | error
//   host  -> shell: play | pause | seek | requestState
// Transport is JSON strings both ways (postMessage(JSON.stringify(...)) / JSON.parse(e.data)) so the
// host reads with TryGetWebMessageAsString. No credentials, cookies, or tokens cross this channel.
(function () {
  "use strict";

  var PROTOCOL_VERSION = 1;
  var host = window.chrome && window.chrome.webview;

  function postToHost(message) {
    message.v = PROTOCOL_VERSION;
    try { if (host) host.postMessage(JSON.stringify(message)); } catch (e) { /* best-effort */ }
  }

  // Only the non-sensitive playback target is carried in the shell URL.
  var params = new URLSearchParams(window.location.search);
  var videoId = params.get("v") || "";
  var listId = params.get("list") || "";
  var startSeconds = parseInt(params.get("start") || "0", 10) || 0;
  var origin = window.location.origin;

  var player = null;
  var stateTimer = null;

  function currentTime() {
    try { return Math.floor(player && player.getCurrentTime ? player.getCurrentTime() : 0); }
    catch (e) { return 0; }
  }

  function duration() {
    try {
      var d = player && player.getDuration ? player.getDuration() : 0;
      return isFinite(d) && d > 0 ? Math.floor(d) : null;
    } catch (e) { return null; }
  }

  function playerState() {
    try { return player && player.getPlayerState ? player.getPlayerState() : -1; }
    catch (e) { return -1; }
  }

  function sendState() {
    postToHost({ type: "state", currentTime: currentTime(), playerState: playerState(), duration: duration() });
  }

  // Called by the YouTube IFrame API once it has loaded (defined before the API script runs).
  window.onYouTubeIframeAPIReady = function () {
    var playerVars = {
      enablejsapi: 1,
      origin: origin,
      autoplay: 1,
      playsinline: 1,
      rel: 0,
      modestbranding: 0,
      start: startSeconds
    };
    if (listId) playerVars.list = listId;

    player = new YT.Player("player", {
      videoId: videoId,
      playerVars: playerVars,
      events: {
        onReady: function () {
          postToHost({ type: "ready" });
          if (stateTimer) clearInterval(stateTimer);
          stateTimer = setInterval(sendState, 250); // matches the host DOM-sync cadence
          sendState();
        },
        onStateChange: function () { sendState(); },
        onError: function (e) {
          postToHost({ type: "error", code: String(e && e.data != null ? e.data : "unknown") });
        }
      }
    });
  };

  // Host -> shell commands.
  if (host) {
    host.addEventListener("message", function (e) {
      var msg;
      try { msg = JSON.parse(e.data); } catch (err) { return; }
      if (!msg || !player) return;
      try {
        switch (msg.type) {
          case "play": player.playVideo(); break;
          case "pause": player.pauseVideo(); break;
          case "seek": if (typeof msg.seconds === "number") player.seekTo(msg.seconds, true); break;
          case "requestState": sendState(); break;
        }
      } catch (err) { /* best-effort: a bad command must never break the shell */ }
    });
  }
})();
