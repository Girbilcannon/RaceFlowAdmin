# 🏁 RaceFlow Admin (GW2)

RaceFlow Admin is a real-time race visualization and control tool built for Guild Wars 2 race events. It connects to live telemetry data, processes racer progress through a defined flow map, and outputs a broadcast-ready overlay for OBS.

---

## ⚠️ Disclaimer

This tool is intended **ONLY for official race administrators**.

All racers participating in events using RaceFlow **must be running the GW2 Telemetry HUD** and connected to the same telemetry server/session.

---

## 🚀 Overview

RaceFlow Admin handles:

- Live racer tracking via WebSocket telemetry  
- Flow map progression (nodes, splits, branches)  
- Broadcast-ready overlay output  
- Theme customization (colors, nodes, lines, labels)  
- Playback delay control (for OBS sync)  
- Admin tuning tools for layout and visuals  

---

## 🔌 Server Connection (Telemetry)

RaceFlow connects to a telemetry server (such as BeetleRank).

### Required Fields

**WebSocket URL**
wss://www.beetlerank.com:3002


**Session / Event Code**
- Must match what racers are using in GW2 Telemetry HUD  
- Can be left blank to view all sessions (not recommended for live events)  

### How it works

1. Racers run GW2 Telemetry HUD  
2. HUD sends position, map, and session data to the server  
3. RaceFlow Admin connects via WebSocket  
4. Incoming snapshots are processed into race state  

---

## 🎥 OBS Overlay (Browser Source)

RaceFlow runs a local web server that provides the broadcast overlay.

### Default URLs
- http://localhost:5057/
- http://localhost:5057/overlay
- http://localhost:5057/overlay-data

### Setup in OBS

1. Add a **Browser Source**  
2. Set URL to: http://localhost:5057/overlay
3. Set resolution (recommended): 1920x1080
4. Enable:
- Shutdown source when not visible  
- Refresh browser when scene becomes active  

### Overlay Features

- Flow map layout  
- Racer positions along paths  
- Branch logic (splits / convergence)  
- Racer names and markers  
- Theme-based styling  

---

## 🧭 Flow Map

Flow maps define:
- Nodes (checkpoints)  
- Edges (connections)  
- Splits and branch logic  
- Sections (screen layout zones)  

Load a flow map using: Load Flow → select .json


---

## 🎨 Themes

Themes control the visual appearance of the overlay.

Themes are stored in: /Themes/


---

## 🧾 Basic Theme Structure

```json
{
  "themeName": "Default Theme",

  "racers": {
    "dotSize": 18,
    "inactiveDotSize": 14,
    "glowScale": 1.8,
    "glowOpacity": 0.28,
    "nameVisible": true,
    "nameScale": 1.0,
    "nameOffsetX": 12,
    "nameOffsetY": -16
  },

  "segments": {
    "default": {
      "color": "#FFFFFF",
      "thickness": 3
    }
  },

  "nodes": {
    "default": {
      "scale": 1.0,
      "titleVisible": true
    }
  }
}
```

---

## 🧩 Override System

Themes support fine-grained overrides for individual elements.

### Node Overrides
```json
"nodeOverrides": {
  "node_id_here": {
    "image": "boss.png",
    "scale": 1.4,
    "titleVisible": false
  }
}
```

Use this to:
- Change indvidual node icons
- Resize specific nodes
- Show or hide labels per node

---

### Segment Overrides
```json
"segmentOverrides": {
  "segment_id_here": {
    "color": "#00FF00",
    "thickness": 4
  }
}
```

Use this to:
- Color-code sections (Example: SAB World 1 vs World 2)
- Emphasize important paths

---

### Race Styling
```json
"racers": {
  "dotSize": 18,
  "inactiveDotSize": 14,
  "nameScale": 1.0
}
```

Controls:
- Racer marker size
- Name size
- Visibilty
- Glow

---

## ⚙️ Admin Controls
### Trigger Scale
Adjusts checkpoint detection radius globally.

### Playback Delay (ms)
Delays overlay rendering to match OBS broadcast delay.

### Output Tuning Window
Allows:
- Section positioning
- Layout adjustment
- Real-time visual tuning

---

## 🧪 Typical Workflow
1. Start RaceFlowAdmin.exe
2. Connect to Telemetry Server
3. Load flow map
4. Select theme
5. Open Output Window
6. Add OBS browser source
7. Adjust Delay and Screenspace tuning if needed
8. Run the event

---

## 📦 Folder Structure (Release)
```
RaceFlow/
│
├── RaceFlowAdmin.exe
│
├── Themes/
│   ├── DefaultTheme.json
│   ├── CustomTheme.json
│
├── samples/
```

---

## 🧠 Notes
- Themes are loaded from disk at runtime
- Changes require relodad or restart to apply
- Flow maps can be edited and saved from the Admin tool

## 🏁 Final Notes
RaceFlow Admin is designed for:
- clarity
- reliability
- broeadcast-ready output

It is not intended as a public spectator tool - It is a race control system with more features to come in teh future. Spectators can see the flow map output from this tool when combined with your video stream in OBS.
