using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Admin
{
    public sealed class OverlayWebServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly object _sync = new();
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _baseUrl;

        private CancellationTokenSource? _cts;
        private Task? _listenTask;

        private OverlayPayload _latestPayload = new();

        public string BaseUrl => _baseUrl;
        public string OverlayUrl => $"{_baseUrl}overlay";
        public string OverlayDataUrl => $"{_baseUrl}overlay-data";

        public OverlayWebServer(int port = 5057)
        {
            _baseUrl = $"http://localhost:{port}/";

            _listener.Prefixes.Add(_baseUrl);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public void Start()
        {
            if (_listener.IsListening)
                return;

            _cts = new CancellationTokenSource();
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void UpdateScene(RuntimeGraph? graph, RaceOutputFrame? frame, string? themeFile)
        {
            var payload = BuildPayload(graph, frame, themeFile);

            lock (_sync)
            {
                _latestPayload = payload;
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    continue;
                }

                _ = Task.Run(() => HandleContextAsync(context), cancellationToken);
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath?.Trim('/')?.ToLowerInvariant() ?? string.Empty;

            try
            {
                if (string.IsNullOrEmpty(path) || path == "overlay")
                {
                    await WriteHtmlAsync(context.Response, GetOverlayHtml());
                    return;
                }

                if (path == "overlay-data")
                {
                    OverlayPayload payload;
                    lock (_sync)
                    {
                        payload = _latestPayload;
                    }

                    await WriteJsonAsync(context.Response, payload);
                    return;
                }

                if (path.StartsWith("themes/", StringComparison.Ordinal))
                {
                    string themesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
                    string relativePath = path.Substring("themes/".Length).Replace('/', Path.DirectorySeparatorChar);
                    string fullPath = Path.Combine(themesRoot, relativePath);

                    if (File.Exists(fullPath))
                    {
                        string ext = Path.GetExtension(fullPath).ToLowerInvariant();

                        string contentType = ext switch
                        {
                            ".png" => "image/png",
                            ".jpg" => "image/jpeg",
                            ".jpeg" => "image/jpeg",
                            ".json" => "application/json",
                            _ => "application/octet-stream"
                        };

                        byte[] fileBytes = await File.ReadAllBytesAsync(fullPath);

                        context.Response.StatusCode = 200;
                        context.Response.ContentType = contentType;
                        context.Response.ContentLength64 = fileBytes.Length;
                        await context.Response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                        return;
                    }
                }

                context.Response.StatusCode = 404;
                await WriteTextAsync(context.Response, "Not found", "text/plain; charset=utf-8");
            }
            catch
            {
                try
                {
                    context.Response.StatusCode = 500;
                    await WriteTextAsync(context.Response, "Server error", "text/plain; charset=utf-8");
                }
                catch
                {
                }
            }
            finally
            {
                try
                {
                    context.Response.OutputStream.Close();
                }
                catch
                {
                }
            }
        }

        private async Task WriteHtmlAsync(HttpListenerResponse response, string html)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private async Task WriteJsonAsync(HttpListenerResponse response, object data)
        {
            string json = JsonSerializer.Serialize(data, _jsonOptions);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = 200;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private async Task WriteTextAsync(HttpListenerResponse response, string text, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            response.ContentType = contentType;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static OverlayPayload BuildPayload(RuntimeGraph? graph, RaceOutputFrame? frame, string? themeFile)
        {
            var payload = new OverlayPayload
            {
                ThemeFile = themeFile
            };

            if (graph != null)
            {
                payload.LayoutName = graph.LayoutName;
                payload.Version = graph.Version;
                payload.ExportedAtUtc = graph.ExportedAtUtc;

                foreach (RuntimeSection section in graph.Sections)
                {
                    payload.Sections.Add(new OverlaySectionPayload
                    {
                        Side = section.Side,
                        DisplayName = section.DisplayName,
                        Direction = section.Direction,
                        VisualScale = section.VisualScale,
                        OffsetX = section.OffsetX,
                        OffsetY = section.OffsetY
                    });
                }

                foreach (RuntimeSegment segment in graph.Segments)
                {
                    payload.Segments.Add(new OverlaySegmentPayload
                    {
                        Id = segment.Id,
                        Index = segment.Index,
                        Label = segment.Label,
                        Side = segment.Side,
                        Direction = segment.Direction,
                        PewFileName = segment.PewFileName,
                        CheckpointFileName = segment.CheckpointFileName
                    });
                }

                foreach (RuntimeNode node in graph.Nodes)
                {
                    payload.Nodes.Add(new OverlayNodePayload
                    {
                        Id = node.Id,
                        Index = node.Index,
                        Label = node.Label,
                        RuntimeLabel = node.RuntimeLabel,
                        NodeType = node.NodeType.ToString().ToLowerInvariant(),
                        SectionSideRaw = node.SectionSideRaw,
                        SegmentId = node.SegmentId,
                        SegmentLabel = node.SegmentLabel,
                        OverlayX = node.OverlayX,
                        OverlayY = node.OverlayY,
                        IgnoreNode = node.IgnoreNode,
                        IsBound = node.IsBound,
                        IsEndOfRace = node.IsEndOfRace,
                        MapId = node.MapId,
                        WorldX = node.WorldX,
                        WorldY = node.WorldY,
                        WorldZ = node.WorldZ,
                        TriggerRadius = node.TriggerRadius,
                        TriggerAngle = node.TriggerAngle
                    });
                }

                foreach (RuntimeEdge edge in graph.Edges)
                {
                    payload.Edges.Add(new OverlayEdgePayload
                    {
                        Id = edge.Id,
                        FromNodeId = edge.FromNodeId,
                        ToNodeId = edge.ToNodeId,
                        FromSocketIndex = edge.FromSocketIndex,
                        ToSocketIndex = edge.ToSocketIndex
                    });
                }
            }

            if (frame != null)
            {
                payload.CapturedUtc = frame.CapturedUtc;

                foreach (RaceOutputRacerVisual racer in frame.Racers)
                {
                    payload.Racers.Add(new OverlayRacerPayload
                    {
                        RacerKey = racer.RacerKey,
                        RacerName = racer.RacerName,
                        ColorHex = racer.ColorHex,
                        IsActive = racer.IsActive,
                        LastConfirmedNodeId = racer.LastConfirmedNodeId,
                        TargetNodeId = racer.TargetNodeId,
                        EdgeProgress = racer.EdgeProgress,
                        HasFinished = racer.HasFinished,
                        StatusText = racer.StatusText
                    });
                }
            }

            return payload;
        }

        private static string GetOverlayHtml()
        {
            return """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>RaceFlow Overlay</title>
<style>
    html, body {
        margin: 0;
        padding: 0;
        width: 100%;
        height: 100%;
        overflow: hidden;
        background: transparent;
        font-family: "Segoe UI", Arial, sans-serif;
    }

    canvas {
        display: block;
        width: 100vw;
        height: 100vh;
        background: transparent;
    }
</style>
</head>
<body>
<canvas id="overlayCanvas"></canvas>

<script>
const canvas = document.getElementById("overlayCanvas");
const ctx = canvas.getContext("2d");

let latestData = null;
let currentTheme = null;
let currentThemeFile = null;
let imageCache = {};

function resizeCanvas() {
    const dpr = Math.max(1, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.floor(window.innerWidth));
    const height = Math.max(1, Math.floor(window.innerHeight));

    canvas.width = Math.floor(width * dpr);
    canvas.height = Math.floor(height * dpr);
    canvas.style.width = width + "px";
    canvas.style.height = height + "px";

    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.scale(dpr, dpr);

    draw();
}

async function loadTheme(themeFile) {
    if (!themeFile)
        return;

    if (currentThemeFile === themeFile && currentTheme)
        return;

    try {
        const response = await fetch("/themes/" + encodeURIComponent(themeFile) + "?ts=" + Date.now(), { cache: "no-store" });
        if (!response.ok)
            return;

        currentTheme = await response.json();
        currentThemeFile = themeFile;
        imageCache = {};

        function queueImage(file) {
            if (!file)
                return;

            if (imageCache[file])
                return;

            const img = new Image();
            img.src = "/themes/" + encodeURIComponent(themeFile.replace(".json", "")) + "/" + file;
            imageCache[file] = img;
        }

        if (currentTheme.nodes) {
            for (const key in currentTheme.nodes) {
                queueImage(currentTheme.nodes[key]);
            }
        }

        if (currentTheme.lines) {
            for (const key in currentTheme.lines) {
                queueImage(currentTheme.lines[key]);
            }
        }

        if (currentTheme.pathDefaults) {
            for (const key in currentTheme.pathDefaults) {
                queueImage(currentTheme.pathDefaults[key]);
            }
        }

        if (currentTheme.nodeOverrides) {
            for (const key in currentTheme.nodeOverrides) {
                const o = currentTheme.nodeOverrides[key];
                if (o && o.image)
                    queueImage(o.image);
            }
        }

        if (currentTheme.segmentOverrides) {
            for (const key in currentTheme.segmentOverrides) {
                const o = currentTheme.segmentOverrides[key];
                if (!o)
                    continue;
                if (o.nodeImage)
                    queueImage(o.nodeImage);
                if (o.lineImage)
                    queueImage(o.lineImage);
            }
        }
    } catch {
        currentTheme = null;
        currentThemeFile = null;
        imageCache = {};
    }
}

window.addEventListener("resize", resizeCanvas);

async function pollData() {
    try {
        const response = await fetch("/overlay-data?ts=" + Date.now(), { cache: "no-store" });
        if (!response.ok)
            throw new Error("HTTP " + response.status);

        latestData = await response.json();

        if (latestData.themeFile) {
            await loadTheme(latestData.themeFile);
        }

        draw();
    } catch {
    }
}

setInterval(pollData, 250);
pollData();
resizeCanvas();

function draw() {
    const width = window.innerWidth;
    const height = window.innerHeight;

    ctx.clearRect(0, 0, width, height);

    if (!latestData || !latestData.nodes || latestData.nodes.length === 0) {
        drawCenteredMessage("No flow loaded.");
        return;
    }

    const sideRegions = buildSideRegions({ left: 0, top: 0, width, height });
    const segmentLayouts = buildSegmentLayouts(latestData, sideRegions);
    const nodeMap = buildNodeMap(latestData.nodes);

    if (Object.keys(segmentLayouts).length === 0) {
        drawCenteredMessage("Flow has no drawable segments.");
        return;
    }

    drawEdges(latestData, nodeMap, segmentLayouts);
    drawNodes(latestData, segmentLayouts);
    drawRacers(latestData, nodeMap, segmentLayouts);
}

function drawCenteredMessage(message) {
    ctx.save();
    ctx.font = "bold 16px Segoe UI";
    ctx.fillStyle = "rgba(220,220,220,0.95)";
    const metrics = ctx.measureText(message);
    ctx.fillText(message, (window.innerWidth - metrics.width) * 0.5, window.innerHeight * 0.5);
    ctx.restore();
}

function buildNodeMap(nodes) {
    const map = {};
    for (const node of nodes)
        map[node.id] = node;
    return map;
}

function buildSideRegions(client) {
    const outerMargin = 16;
    const full = {
        left: outerMargin,
        top: outerMargin,
        width: Math.max(1, client.width - (outerMargin * 2)),
        height: Math.max(1, client.height - (outerMargin * 2))
    };

    const sideThickness = Math.max(110, full.width * 0.16);
    const topBottomThickness = Math.max(110, full.height * 0.16);

    return {
        left: {
            left: full.left,
            top: full.top,
            width: sideThickness,
            height: full.height
        },
        top: {
            left: full.left,
            top: full.top,
            width: full.width,
            height: topBottomThickness
        },
        right: {
            left: full.left + full.width - sideThickness,
            top: full.top,
            width: sideThickness,
            height: full.height
        },
        bottom: {
            left: full.left,
            top: full.top + full.height - topBottomThickness,
            width: full.width,
            height: topBottomThickness
        }
    };
}

function buildSegmentLayouts(data, sideRegions) {
    const result = {};

    for (const section of data.sections || []) {
        const side = getSideOrUnknown(section.side);
        if (side === "unknown")
            continue;

        const sideRect = sideRegions[side];
        if (!sideRect)
            continue;

        const sectionSegments = (data.segments || [])
            .filter(s => getSideOrUnknown(s.side) === side)
            .sort((a, b) => a.index - b.index);

        if (sectionSegments.length === 0)
            continue;

        for (let i = 0; i < sectionSegments.length; i++) {
            const segment = sectionSegments[i];

            const segmentNodes = (data.nodes || [])
                .filter(n => equalsIgnoreCase(n.segmentId || "", segment.id || ""))
                .sort((a, b) => a.index - b.index);

            if (segmentNodes.length === 0)
                continue;

            const effectiveDirection = getEffectiveDirection(section.direction, segment.direction, side);
            const sourceBounds = getTransformedSourceBounds(segmentNodes, side, effectiveDirection);
            const targetRect = getSegmentTargetRect(sideRect, side, effectiveDirection, i, sectionSegments.length);

            result[segment.id] = new SegmentLayout(section, side, effectiveDirection, targetRect, sourceBounds);
        }
    }

    return result;
}

function getEffectiveDirection(sectionDirection, segmentDirection, side) {
    const segment = (segmentDirection || "").trim();
    if (segment.length > 0)
        return segment;

    const section = (sectionDirection || "").trim();
    if (section.length > 0)
        return section;

    switch (side) {
        case "left":
        case "right":
            return "top-to-bottom";
        case "top":
        case "bottom":
            return "left-to-right";
        default:
            return "";
    }
}

function getSegmentTargetRect(sideRect, side, direction, segmentIndex, segmentCount) {
    const innerPadX = 6;
    const innerPadY = 6;

    const contentRect = {
        left: sideRect.left + innerPadX,
        top: sideRect.top + innerPadY,
        width: Math.max(1, sideRect.width - (innerPadX * 2)),
        height: Math.max(1, sideRect.height - (innerPadY * 2))
    };

    let slotIndex = segmentIndex;

    if (side === "left" || side === "right") {
        const reverseSlotOrder = equalsIgnoreCase(direction, "bottom-to-top");
        if (reverseSlotOrder)
            slotIndex = (segmentCount - 1) - segmentIndex;

        const segmentHeight = contentRect.height / Math.max(1, segmentCount);

        return {
            left: contentRect.left,
            top: contentRect.top + (segmentHeight * slotIndex),
            width: contentRect.width,
            height: Math.max(1, segmentHeight)
        };
    } else {
        const reverseSlotOrder = equalsIgnoreCase(direction, "right-to-left");
        if (reverseSlotOrder)
            slotIndex = (segmentCount - 1) - segmentIndex;

        const segmentWidth = contentRect.width / Math.max(1, segmentCount);

        return {
            left: contentRect.left + (segmentWidth * slotIndex),
            top: contentRect.top,
            width: Math.max(1, segmentWidth),
            height: contentRect.height
        };
    }
}

function getTransformedSourceBounds(nodes, side, direction) {
    const points = nodes.map(node => transformRawPoint(node.overlayX || 0, node.overlayY || 0, side));

    const minX = Math.min(...points.map(p => p.x));
    const minY = Math.min(...points.map(p => p.y));
    const maxX = Math.max(...points.map(p => p.x));
    const maxY = Math.max(...points.map(p => p.y));

    const orientedBounds = {
        left: minX,
        top: minY,
        width: Math.max(1, maxX - minX),
        height: Math.max(1, maxY - minY)
    };

    const directedPoints = points.map(p => applyDirectionFlip(p, orientedBounds, side, direction));

    const directedMinX = Math.min(...directedPoints.map(p => p.x));
    const directedMinY = Math.min(...directedPoints.map(p => p.y));
    const directedMaxX = Math.max(...directedPoints.map(p => p.x));
    const directedMaxY = Math.max(...directedPoints.map(p => p.y));

    return {
        left: directedMinX,
        top: directedMinY,
        width: Math.max(1, directedMaxX - directedMinX),
        height: Math.max(1, directedMaxY - directedMinY)
    };
}

function transformRawPoint(x, y, side) {
    if (side === "left" || side === "right")
        return { x: y, y: x };

    return { x, y };
}

function applyDirectionFlip(point, bounds, side, direction) {
    const vertical = side === "left" || side === "right";

    let x = point.x;
    let y = point.y;

    if (vertical) {
        if (equalsIgnoreCase(direction, "bottom-to-top")) {
            const relativeY = y - bounds.top;
            y = bounds.top + (bounds.height - relativeY);
        }
    } else {
        if (equalsIgnoreCase(direction, "right-to-left")) {
            const relativeX = x - bounds.left;
            x = bounds.left + (bounds.width - relativeX);
        }
    }

    return { x, y };
}

function drawEdges(data, nodeMap, segmentLayouts) {
    const renderMode = getLineRenderMode();

    for (const edge of data.edges || []) {
        const fromNode = nodeMap[edge.fromNodeId];
        const toNode = nodeMap[edge.toNodeId];

        if (!fromNode || !toNode)
            continue;

        const aCenter = transformNode(fromNode, segmentLayouts);
        const bCenter = transformNode(toNode, segmentLayouts);

        const trimmed = getTrimmedEdgePoints(fromNode, toNode, aCenter, bCenter);
        const a = trimmed.a;
        const b = trimmed.b;

        const lineStyle = getResolvedLineStyle(fromNode, toNode);

        if (renderMode === "textured") {
            const img = lineStyle.imageFile ? imageCache[lineStyle.imageFile] : null;

            if (img && img.complete) {
                const dx = b.x - a.x;
                const dy = b.y - a.y;
                const length = Math.hypot(dx, dy);
                const angle = Math.atan2(dy, dx);
                const step = Math.max(8, (lineStyle.step || 32));
                const texSize = Math.max(8, (lineStyle.textureSize || 32));

                ctx.save();
                ctx.translate(a.x, a.y);
                ctx.rotate(angle);

                for (let i = 0; i < length; i += step) {
                    ctx.save();
                    ctx.translate(i, 0);
                    ctx.rotate(Math.PI / 2);
                    ctx.drawImage(img, -(texSize * 0.5), -(texSize * 0.5), texSize, texSize);
                    ctx.restore();
                }

                ctx.restore();
                continue;
            }
        }

        ctx.save();

        if (isShadowEnabled()) {
            ctx.strokeStyle = `rgba(0,0,0,${getShadowOpacity()})`;
            ctx.lineWidth = Math.max(1, lineStyle.thickness + 2);
            ctx.lineCap = "round";
            ctx.shadowColor = `rgba(0,0,0,${getShadowOpacity()})`;
            ctx.shadowBlur = getShadowBlur();
            ctx.beginPath();
            ctx.moveTo(a.x + getShadowOffsetX(), a.y + getShadowOffsetY());
            ctx.lineTo(b.x + getShadowOffsetX(), b.y + getShadowOffsetY());
            ctx.stroke();

            ctx.shadowColor = "transparent";
            ctx.shadowBlur = 0;
        }

        ctx.strokeStyle = lineStyle.color;
        ctx.lineWidth = lineStyle.thickness;
        ctx.lineCap = "round";
        ctx.beginPath();
        ctx.moveTo(a.x, a.y);
        ctx.lineTo(b.x, b.y);
        ctx.stroke();

        ctx.restore();
    }
}

function drawNodes(data, segmentLayouts) {
    for (const node of (data.nodes || []).slice().sort((a, b) => a.index - b.index)) {
        const p = transformNode(node, segmentLayouts);
        const nodeStyle = getResolvedNodeStyle(node);
        const size = nodeStyle.size;
        const left = p.x - (size * 0.5);
        const top = p.y - (size * 0.5);
        const img = nodeStyle.imageFile ? imageCache[nodeStyle.imageFile] : null;

        ctx.save();

        if (img && img.complete) {
            ctx.drawImage(img, left, top, size, size);
        } else {
            ctx.fillStyle = nodeStyle.color;

            if ((node.nodeType || "") === "split" || (node.nodeType || "") === "converge") {
                ctx.fillRect(left, top, size, size);
                ctx.strokeStyle = "rgba(230,240,250,1)";
                ctx.lineWidth = 2;
                ctx.strokeRect(left, top, size, size);
            } else {
                ctx.beginPath();
                ctx.arc(p.x, p.y, size * 0.5, 0, Math.PI * 2);
                ctx.fill();

                ctx.strokeStyle = "rgba(230,240,250,1)";
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.arc(p.x, p.y, size * 0.5, 0, Math.PI * 2);
                ctx.stroke();
            }
        }

        if (nodeStyle.titleVisible) {
            ctx.fillStyle = "rgba(255,255,255,1)";
            ctx.font = `bold ${Math.max(8, 9 * nodeStyle.titleScale)}px Segoe UI`;
            ctx.textAlign = "center";
            ctx.textBaseline = "top";
            ctx.fillText(node.label || "", p.x + nodeStyle.titleOffsetX, p.y + (size * 0.5) + 4 + nodeStyle.titleOffsetY);
        }

        ctx.restore();
    }
}

function drawRacers(data, nodeMap, segmentLayouts) {
    const racers = data.racers || [];
    if (racers.length === 0)
        return;

    const racerTheme = currentTheme?.racers || {};
    const activeDotSize = Math.max(2, toNumber(racerTheme.dotSize, 18));
    const inactiveDotSize = Math.max(2, toNumber(racerTheme.inactiveDotSize, 14));
    const glowScale = Math.max(1, toNumber(racerTheme.glowScale, 1.8));
    const glowOpacity = clamp(toNumber(racerTheme.glowOpacity, 0.28), 0, 1);
    const nameVisible = racerTheme.nameVisible !== false;
    const nameScale = Math.max(0.25, toNumber(racerTheme.nameScale, 1.0));
    const nameOffsetX = toNumber(racerTheme.nameOffsetX, 12);
    const nameOffsetY = toNumber(racerTheme.nameOffsetY, -16);

    for (const racer of racers) {
        const point = getRacerPoint(racer, nodeMap, segmentLayouts);
        if (!point)
            continue;

        const fillColor = parseColor(racer.colorHex || "#FFFFFF");
        const dotSize = racer.isActive ? activeDotSize : inactiveDotSize;
        const glowRadius = dotSize * glowScale;

        ctx.save();

        ctx.fillStyle = withAlpha(fillColor, glowOpacity);
        ctx.beginPath();
        ctx.arc(point.x, point.y, glowRadius * 0.5, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = fillColor;
        ctx.beginPath();
        ctx.arc(point.x, point.y, dotSize * 0.5, 0, Math.PI * 2);
        ctx.fill();

        ctx.strokeStyle = "rgba(255,255,255,1)";
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(point.x, point.y, dotSize * 0.5, 0, Math.PI * 2);
        ctx.stroke();

        if (nameVisible) {
            ctx.font = `bold ${Math.max(1, 8.5 * nameScale)}px Segoe UI`;
            ctx.textAlign = "left";
            ctx.textBaseline = "top";
            ctx.fillStyle = "rgba(255,255,255,1)";
            ctx.fillText(racer.racerName || "", point.x + nameOffsetX, point.y + nameOffsetY);
        }

        ctx.restore();
    }
}

function getRacerPoint(racer, nodeMap, segmentLayouts) {
    if (racer.lastConfirmedNodeId && racer.targetNodeId &&
        nodeMap[racer.lastConfirmedNodeId] &&
        nodeMap[racer.targetNodeId]) {
        const fromNode = nodeMap[racer.lastConfirmedNodeId];
        const toNode = nodeMap[racer.targetNodeId];

        const a = transformNode(fromNode, segmentLayouts);
        const b = transformNode(toNode, segmentLayouts);

        const t = Math.max(0, Math.min(1, Number(racer.edgeProgress || 0)));

        return {
            x: a.x + ((b.x - a.x) * t),
            y: a.y + ((b.y - a.y) * t)
        };
    }

    if (racer.lastConfirmedNodeId && nodeMap[racer.lastConfirmedNodeId])
        return transformNode(nodeMap[racer.lastConfirmedNodeId], segmentLayouts);

    return null;
}

function transformNode(node, segmentLayouts) {
    const layout = segmentLayouts[node.segmentId];
    if (!layout)
        return { x: 0, y: 0 };

    return layout.transform(node);
}

function getResolvedNodeStyle(node) {
    const settings = currentTheme?.settings || {};
    const segmentOverride = getSegmentOverride(node.segmentId);
    const nodeOverride = getNodeOverride(node.id);

    const baseScale = toNumber(settings.nodeScale, 1.0);
    const segmentScale = segmentOverride && segmentOverride.nodeScale !== undefined ? toNumber(segmentOverride.nodeScale, 1.0) : 1.0;
    const overrideScale = nodeOverride && nodeOverride.scale !== undefined ? toNumber(nodeOverride.scale, 1.0) : 1.0;

    const size = getBaseNodeSize(node) * baseScale * segmentScale * overrideScale;

    let imageFile = null;
    if (nodeOverride && nodeOverride.image)
        imageFile = nodeOverride.image;
    else if (segmentOverride && segmentOverride.nodeImage)
        imageFile = segmentOverride.nodeImage;
    else if (currentTheme?.nodes?.[node.nodeType])
        imageFile = currentTheme.nodes[node.nodeType];
    else if (currentTheme?.nodes?.checkpoint)
        imageFile = currentTheme.nodes.checkpoint;

    const globalTitleVisible = settings.titleVisible !== false;
    let titleVisible = globalTitleVisible;

    if (segmentOverride && segmentOverride.titleVisible !== undefined)
        titleVisible = Boolean(segmentOverride.titleVisible);
    if (nodeOverride && nodeOverride.titleVisible !== undefined)
        titleVisible = Boolean(nodeOverride.titleVisible);

    const titleScale = nodeOverride && nodeOverride.titleScale !== undefined
        ? toNumber(nodeOverride.titleScale, 1.0)
        : (segmentOverride && segmentOverride.titleScale !== undefined
            ? toNumber(segmentOverride.titleScale, 1.0)
            : toNumber(settings.titleScale, 1.0));

    const titleOffsetX = nodeOverride && nodeOverride.titleOffsetX !== undefined
        ? toNumber(nodeOverride.titleOffsetX, 0)
        : (segmentOverride && segmentOverride.titleOffsetX !== undefined
            ? toNumber(segmentOverride.titleOffsetX, 0)
            : toNumber(settings.titleOffsetX, 0));

    const titleOffsetY = nodeOverride && nodeOverride.titleOffsetY !== undefined
        ? toNumber(nodeOverride.titleOffsetY, 0)
        : (segmentOverride && segmentOverride.titleOffsetY !== undefined
            ? toNumber(segmentOverride.titleOffsetY, 0)
            : toNumber(settings.titleOffsetY, 0));

    let color = getNodeColor(node);
    if (nodeOverride && nodeOverride.tintColor)
        color = parseColor(nodeOverride.tintColor);
    else if (segmentOverride && segmentOverride.nodeTintColor)
        color = parseColor(segmentOverride.nodeTintColor);

    return {
        size,
        imageFile,
        color,
        titleVisible,
        titleScale,
        titleOffsetX,
        titleOffsetY
    };
}

function getResolvedLineStyle(fromNode, toNode) {
    const settings = currentTheme?.settings || {};
    const renderMode = getLineRenderMode();

    const fromSegmentOverride = getSegmentOverride(fromNode.segmentId);
    const toSegmentOverride = getSegmentOverride(toNode.segmentId);
    const segmentOverride = fromSegmentOverride || toSegmentOverride;

    let color = getThemeLineColorByNodeType(fromNode, toNode);
    if (segmentOverride && segmentOverride.lineColor)
        color = parseColor(segmentOverride.lineColor);

    let thickness = toNumber(currentTheme?.lines?.thickness, 3);
    thickness *= toNumber(settings.lineScale, 1.0);

    if (segmentOverride && segmentOverride.lineThickness !== undefined)
        thickness = toNumber(segmentOverride.lineThickness, thickness);

    let imageFile = null;
    if (segmentOverride && segmentOverride.lineImage)
        imageFile = segmentOverride.lineImage;
    else if (renderMode === "textured") {
        if ((fromNode.nodeType || "") === "split" && currentTheme?.lines?.split)
            imageFile = currentTheme.lines.split;
        else if ((toNode.nodeType || "") === "converge" && currentTheme?.lines?.converge)
            imageFile = currentTheme.lines.converge;
        else if (currentTheme?.lines?.default)
            imageFile = currentTheme.lines.default;
    }

    const step = segmentOverride && segmentOverride.lineStep !== undefined
        ? toNumber(segmentOverride.lineStep, 32)
        : 32 + toNumber(settings.lineSpacing, 0);

    return {
        color,
        thickness,
        imageFile,
        step,
        textureSize: Math.max(8, thickness * 8)
    };
}

function getNodeOverride(nodeId) {
    if (!currentTheme || !currentTheme.nodeOverrides || !nodeId)
        return null;
    return currentTheme.nodeOverrides[nodeId] || null;
}

function getSegmentOverride(segmentId) {
    if (!currentTheme || !currentTheme.segmentOverrides || !segmentId)
        return null;
    return currentTheme.segmentOverrides[segmentId] || null;
}

function getBaseNodeSize(node) {
    switch ((node.nodeType || "").toLowerCase()) {
        case "start": return 20;
        case "end": return 22;
        case "boss": return 24;
        case "split": return 22;
        case "converge": return 22;
        default: return 16;
    }
}

function getLineRenderMode() {
    const mode = String(currentTheme?.lines?.renderMode || "drawn").toLowerCase();
    return mode === "textured" ? "textured" : "drawn";
}

function getNodeRadius(node) {
    const style = getResolvedNodeStyle(node);
    return style.size * 0.5;
}

function getTrimmedEdgePoints(fromNode, toNode, aCenter, bCenter) {
    const dx = bCenter.x - aCenter.x;
    const dy = bCenter.y - aCenter.y;
    const length = Math.hypot(dx, dy);

    if (length <= 0.001) {
        return {
            a: { x: aCenter.x, y: aCenter.y },
            b: { x: bCenter.x, y: bCenter.y }
        };
    }

    const ux = dx / length;
    const uy = dy / length;

    const startTrim = getNodeRadius(fromNode);
    const endTrim = getNodeRadius(toNode);

    let ax = aCenter.x + (ux * startTrim);
    let ay = aCenter.y + (uy * startTrim);
    let bx = bCenter.x - (ux * endTrim);
    let by = bCenter.y - (uy * endTrim);

    const trimmedLength = Math.hypot(bx - ax, by - ay);
    if (trimmedLength <= 0.001) {
        ax = aCenter.x;
        ay = aCenter.y;
        bx = bCenter.x;
        by = bCenter.y;
    }

    return {
        a: { x: ax, y: ay },
        b: { x: bx, y: by }
    };
}

function getNodeColor(node) {
    switch ((node.nodeType || "").toLowerCase()) {
        case "start": return "rgba(44,180,90,1)";
        case "checkpoint": return "rgba(70,150,225,1)";
        case "split": return "rgba(225,150,55,1)";
        case "converge": return "rgba(160,95,210,1)";
        case "end": return "rgba(205,70,70,1)";
        case "boss": return "rgba(220,60,120,1)";
        default: return "rgba(128,128,128,1)";
    }
}

function getThemeLineColorByNodeType(fromNode, toNode) {
    if ((fromNode.nodeType || "") === "split")
        return getThemeLineColor("split", "rgba(170,130,60,1)");

    if ((toNode.nodeType || "") === "converge")
        return getThemeLineColor("converge", "rgba(140,90,170,1)");

    return getThemeLineColor("default", "rgba(95,120,140,1)");
}

function getSideOrUnknown(rawSide) {
    const text = (rawSide || "").trim().toLowerCase();
    switch (text) {
        case "left":
        case "top":
        case "right":
        case "bottom":
            return text;
        default:
            return "unknown";
    }
}

function parseColor(hex) {
    let text = (hex || "").trim();
    if (text.startsWith("#"))
        text = text.substring(1);

    if (text.length !== 6)
        return "rgba(255,255,255,1)";

    const r = parseInt(text.substring(0, 2), 16);
    const g = parseInt(text.substring(2, 4), 16);
    const b = parseInt(text.substring(4, 6), 16);

    if (Number.isNaN(r) || Number.isNaN(g) || Number.isNaN(b))
        return "rgba(255,255,255,1)";

    return `rgba(${r},${g},${b},1)`;
}

function getThemeLineColor(type, fallback) {
    if (!currentTheme || !currentTheme.lines)
        return fallback;

    if (type === "split" && currentTheme.lines.splitColor)
        return parseColor(currentTheme.lines.splitColor);

    if (type === "converge" && currentTheme.lines.convergeColor)
        return parseColor(currentTheme.lines.convergeColor);

    if (currentTheme.lines.defaultColor)
        return parseColor(currentTheme.lines.defaultColor);

    return fallback;
}

function isShadowEnabled() {
    return Boolean(currentTheme?.settings?.shadowEnabled);
}

function getShadowOpacity() {
    return Number(currentTheme?.settings?.shadowOpacity ?? 0.5);
}

function getShadowBlur() {
    return Number(currentTheme?.settings?.shadowBlur ?? 12);
}

function getShadowOffsetX() {
    return Number(currentTheme?.settings?.shadowOffsetX ?? 4);
}

function getShadowOffsetY() {
    return Number(currentTheme?.settings?.shadowOffsetY ?? 4);
}

function withAlpha(rgbaText, alpha) {
    const match = rgbaText.match(/^rgba\((\d+),(\d+),(\d+),/i);
    if (!match)
        return rgbaText;
    return `rgba(${match[1]},${match[2]},${match[3]},${alpha})`;
}

function equalsIgnoreCase(a, b) {
    return String(a || "").toLowerCase() === String(b || "").toLowerCase();
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function toNumber(value, fallback) {
    const n = Number(value);
    return Number.isFinite(n) ? n : fallback;
}

class SegmentLayout {
    constructor(section, side, direction, targetRect, sourceBounds) {
        this.section = section;
        this.side = side;
        this.direction = direction;
        this.targetRect = targetRect;
        this.sourceBounds = sourceBounds;

        const usableWidth = Math.max(1, targetRect.width - 8);
        const usableHeight = Math.max(1, targetRect.height - 8);

        const scaleX = usableWidth / Math.max(1, sourceBounds.width);
        const scaleY = usableHeight / Math.max(1, sourceBounds.height);

        const baseScale = Math.min(scaleX, scaleY);

        this.scale = baseScale * Number(section.visualScale ?? 1.0);

        const scaledWidth = sourceBounds.width * this.scale;
        const scaledHeight = sourceBounds.height * this.scale;

        this.offsetX =
            targetRect.left +
            ((targetRect.width - scaledWidth) * 0.5) -
            (sourceBounds.left * this.scale) +
            Number(section.offsetX ?? 0);

        this.offsetY =
            targetRect.top +
            ((targetRect.height - scaledHeight) * 0.5) -
            (sourceBounds.top * this.scale) +
            Number(section.offsetY ?? 0);
    }

    transform(node) {
        let point = transformRawPoint(Number(node.overlayX || 0), Number(node.overlayY || 0), this.side);
        point = applyDirectionFlip(point, this.sourceBounds, this.side, this.direction);

        return {
            x: (point.x * this.scale) + this.offsetX,
            y: (point.y * this.scale) + this.offsetY
        };
    }
}
</script>
</body>
</html>
""";
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch
            {
            }

            try
            {
                _listener.Close();
            }
            catch
            {
            }

            try
            {
                _listenTask?.Wait(250);
            }
            catch
            {
            }

            _cts?.Dispose();
        }

        private sealed class OverlayPayload
        {
            public string LayoutName { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string? ThemeFile { get; set; }
            public DateTime ExportedAtUtc { get; set; }
            public DateTime CapturedUtc { get; set; }

            public List<OverlaySectionPayload> Sections { get; } = new();
            public List<OverlaySegmentPayload> Segments { get; } = new();
            public List<OverlayNodePayload> Nodes { get; } = new();
            public List<OverlayEdgePayload> Edges { get; } = new();
            public List<OverlayRacerPayload> Racers { get; } = new();
        }

        private sealed class OverlaySectionPayload
        {
            public string Side { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public float VisualScale { get; set; } = 1.0f;
            public float OffsetX { get; set; }
            public float OffsetY { get; set; }
        }

        private sealed class OverlaySegmentPayload
        {
            public string Id { get; set; } = string.Empty;
            public int Index { get; set; }
            public string Label { get; set; } = string.Empty;
            public string Side { get; set; } = string.Empty;
            public string Direction { get; set; } = string.Empty;
            public string PewFileName { get; set; } = string.Empty;
            public string CheckpointFileName { get; set; } = string.Empty;
        }

        private sealed class OverlayNodePayload
        {
            public string Id { get; set; } = string.Empty;
            public int Index { get; set; }
            public string Label { get; set; } = string.Empty;
            public string RuntimeLabel { get; set; } = string.Empty;
            public string NodeType { get; set; } = string.Empty;
            public string SectionSideRaw { get; set; } = string.Empty;
            public string SegmentId { get; set; } = string.Empty;
            public string SegmentLabel { get; set; } = string.Empty;
            public float OverlayX { get; set; }
            public float OverlayY { get; set; }
            public bool IgnoreNode { get; set; }
            public bool IsBound { get; set; }
            public bool IsEndOfRace { get; set; }
            public int? MapId { get; set; }
            public double WorldX { get; set; }
            public double WorldY { get; set; }
            public double WorldZ { get; set; }
            public double TriggerRadius { get; set; }
            public double TriggerAngle { get; set; }
        }

        private sealed class OverlayEdgePayload
        {
            public string Id { get; set; } = string.Empty;
            public string FromNodeId { get; set; } = string.Empty;
            public string ToNodeId { get; set; } = string.Empty;
            public int FromSocketIndex { get; set; }
            public int ToSocketIndex { get; set; }
        }

        private sealed class OverlayRacerPayload
        {
            public string RacerKey { get; set; } = string.Empty;
            public string RacerName { get; set; } = string.Empty;
            public string ColorHex { get; set; } = "#FFFFFF";
            public bool IsActive { get; set; }
            public string? LastConfirmedNodeId { get; set; }
            public string? TargetNodeId { get; set; }
            public double EdgeProgress { get; set; }
            public bool HasFinished { get; set; }
            public string StatusText { get; set; } = string.Empty;
        }
    }
}
