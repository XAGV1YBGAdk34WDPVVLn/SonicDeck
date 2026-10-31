# SonicDeck — System Audio Controller, Parametric EQ & Real-Time Visualizer

**SonicDeck** is a hardware-accelerated Windows desktop audio control deck built with **.NET 8 (WPF)**, **NAudio (WASAPI)**, and **SkiaSharp**. It combines a 60 FPS real-time spectrum/oscilloscope visualizer, a 10-band parametric/graphic equalizer with dual DSP engines, and a per-application system volume mixer.

---

## ✨ Key Features

### 1. 60 FPS SkiaSharp Real-Time Audio Visualizer
Captures system loopback audio via Windows WASAPI with zero audio interruption and renders 5 interactive visual modes:
1. **Pro Studio RTA + Live EQ (FabFilter Style)** — Interactive 10-band EQ curve overlay where you can drag nodes horizontally (Frequency 20 Hz – 19.5 kHz), vertically (Gain ±15 dB), or scroll the mouse wheel (Q-Factor 0.30 – 8.00). Includes 4 RTA sub-styles:
   - *Mid/Side Stereo + Waterfall Strip*
   - *Micro-Bars + Liquid Spline + Waterfall*
   - *Clean Liquid Spline + EQ Curves*
   - *Deep Waterfall Spectrogram + RTA*
2. **Winamp Classic Segmented LED Bars** — 64-band retro segmented LED analyzer with gravity peak-hold caps.
3. **Harmonic Stereo Oscilloscope + XY Vectorscope** — Dual time-domain waveform plus a live Lissajous phase/stereo imaging scope.
4. **Radial Bass Reactor + Particle Ring** — 360° circular spectrum analyzer that pulses with real-time sub-bass energy.
5. **Cyber Horizon 3D Synthwave Terrain** — Perspective 3D scrolling frequency terrain grid.

- **19 Multi-Stop Color Themes** (with live swatch previews & quick-step controls): *Cyberpunk Neon*, *Pro Studio Mastering*, *Chromatic Prism (Full Spectrum Rainbow)*, *RGB Chameleon Flow (Live Animated Color Shift)*, *Tokyo Midnight*, *Volcanic Magma*, *Bioluminescent Abyss*, *Northern Lights*, *Ultraviolet Laser*, *Solar Eclipse 24K*, *Blood Moon Noir*, *Vaporwave 1995*, *Radioactive Hazard*, *Cosmic Supernova*, *Sunset Synthwave*, *Arctic Borealis*, *Studio Pro Amber*, *Matrix Phosphor*, and *Winamp Classic LED*.
- **Pop-Out / Fullscreen Visualizer Window**: Detach the visualizer into a dedicated multi-monitor window (`F11` or double-click to toggle borderless fullscreen).

### 2. Dual-Engine 10-Band Parametric & Graphic Equalizer
- **10 Interactive Bands** (`31 Hz` Low-Shelf, `62 Hz`, `125 Hz`, `250 Hz`, `500 Hz`, `1 kHz`, `2 kHz`, `4 kHz`, `8 kHz` Peaking, and `16 kHz` High-Shelf) with individual Gain, Frequency, and Q-Factor controls.
- **Master Tone & Preamp Controls**:
  - **Preamp** (`-15 dB` to `+15 dB`) with **Auto-Gain Headroom Protection** to prevent digital clipping.
  - **Sub-Bass Boost** (`75 Hz` Low-Shelf) & **Treble Air Tilt** (`6.5 kHz` High-Shelf).
  - **Global Q Width** multiplier (`0.35x` to `2.50x`) and **Stereo L/R Balance**.
- **10 Built-in Studio & Gaming Presets**:
  - *Flat (Bypass Reference)*, *Bass Boost + Punch*, *Rock / Metal Drive*, *Electronic / Sub Club*, *Vocal & Podcast Clarity*, *V-Shape Dynamic (Winamp Classic)*, *Harman / Studio Target*, *Warm Analog Tube*, *Late Night / Quiet Listening*, and *FPS Gaming (Footsteps & Cues)*.

### 3. Per-App Windows Audio Session Mixer
- Automatically discovers active Windows audio sessions (`WASAPI AudioSessionManager`), showing per-application live peak meters, volume sliders (`0–100%`), and mute toggles alongside Master System Volume.

---

## 🚀 Prerequisites & Setup

### System Requirements
- **OS**: Windows 10 or Windows 11 (x64)
- **.NET Runtime / SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (if building from source)

### Setting Up System-Wide EQ (Choose One or Both)

SonicDeck supports two real-time EQ processing backends selectable from the top bar (**Both**, **Equalizer APO**, or **Internal C# DSP**):

#### Option A: Equalizer APO (Recommended for Zero-Latency System-Wide EQ)
Windows does not allow user-mode apps to intercept global audio in-place without an Audio Processing Object (APO) driver.
1. Download and install **[Equalizer APO](https://sourceforge.net/projects/equalizerapo/)**.
2. During installation, enable Equalizer APO on your primary playback device in the **Configurator** and reboot if prompted.
3. Launch **SonicDeck** (run as Administrator if `C:\Program Files\EqualizerAPO\config` requires elevated write permissions).
4. SonicDeck automatically links `sonicdeck_eq.txt` into Equalizer APO's `config.txt` and updates filter coefficients in real time as you move sliders or drag EQ nodes.

#### Option B: Internal C# DSP Engine (Standalone / Virtual Cable / Built-in Synth)
Works out of the box without Equalizer APO installed:
- **Built-in Test Synth (`🎹 Test Synth`)**: Click the **Test Synth** button in the top bar to play a procedural 118 BPM synthwave groove directly through SonicDeck's internal C# 10-band BiQuad DSP chain so you can audition EQ changes immediately.
- **Live Audio Device Routing**: Route any capture/loopback or virtual audio cable input (e.g., [VB-Audio Virtual Cable](https://vb-audio.com/Cable/)) through the C# DSP chain to any physical headphones/speakers using the **Input → Output** selector at the bottom of the EQ panel.

---

## 🛠️ How to Build & Run

### 1. Clone the Repository
```bash
git clone https://github.com/XAGV1YBGAdk34WDPVVLn/SonicDeck.git
cd SonicDeck
```

### 2. Run Directly in Development Mode
```powershell
dotnet run -c Release
```

### 3. Build a Standalone Portable `.exe` (No .NET Installation Required on Target PC)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
Once published, run `publish\SonicDeck.exe` directly or use `run-sonicdeck.bat`.

---

## 🎛️ Controls & Shortcuts

| Control / Action | Description |
| :--- | :--- |
| **Drag EQ Node (RTA Mode)** | Drag horizontally to sweep frequency (`20 Hz – 19.5 kHz`) and vertically to adjust gain (`±15 dB`) |
| **Mouse Wheel on EQ Node** | Scroll up/down over any node in RTA Mode to narrow or widen its **Q-Factor** (`0.30 – 8.00`) |
| **Right-Click EQ Node** | Reset that band to its default center frequency, `0.0 dB` gain, and `1.41` Q |
| **Double-Click Visualizer** | Open the detached Pop-Out Visualizer window (or toggle fullscreen inside the pop-out window) |
| **`F11` / `Esc` (Pop-Out)** | Toggle borderless fullscreen mode in the Pop-Out Visualizer window |
| **`T` (Pop-Out / Fullscreen)** | Cycle through all 19 color themes in the Pop-Out / Fullscreen Visualizer window |

---

## 📁 Project Structure

```text
SonicDeck/
├── App.xaml / App.xaml.cs               # Dark studio theme resources & startup lifecycle
├── MainWindow.xaml / .cs                # Main control deck UI, interactive EQ & mixer bindings
├── VisualizerPopoutWindow.xaml / .cs    # Detached multi-monitor / fullscreen visualizer window
├── Audio/
│   ├── AudioAnalysisEngine.cs           # WASAPI Loopback capture, 4096-pt FFT, LUFS & BPM tracker
│   ├── SystemAudioMixerService.cs       # Per-application Windows audio session controller
│   ├── Dsp/
│   │   └── BiquadEqChain.cs             # 10-band RBJ BiQuad DSP filter cascade + magnitude response
│   └── EqEngines/
│       ├── EqualizerApoService.cs       # Live Equalizer APO config.txt / sonicdeck_eq.txt sync
│       └── InternalDspRouter.cs         # WASAPI Input->DSP->Output router & procedural test synth
├── Models/
│   └── EqModels.cs                      # Parametric EQ band models & 10 built-in presets
└── Visuals/
    ├── AppIconGenerator.cs              # Procedural multi-resolution icon generator
    ├── SkiaVisualizerRenderer.cs        # 60 FPS SkiaSharp renderer (5 modes, 4 RTA styles)
    └── VisualizerModels.cs              # Color palettes & theme definitions
```
