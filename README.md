<h1 align="center">
  <img src="./Assets/logo.png" width="50" align="center">
  <br/>
  LucaLights
</h1>

<h3 align="center">Bring some color to your DDR sessions!</h3>

---

## 🌈 What is this project?

**LucaLights** aims to reduce the cost and complexity of lighting setups for **StepMania** and its forks (like **ITGMania**).
**How?** By using [**WLED**](https://kno.wled.ge/) and the **DDP protocol**, you can repurpose existing LED bulbs and light strips—no expensive proprietary gear required. Adding more lights is as easy as plugging in one of the many affordable controllers supported by WLED.

---

## ⚙️ Basic Setup Demo

> All you need is the IP address of your WLED device—LucaLights handles the rest.

[https://github.com/user-attachments/assets/bb3a2267-fd53-443d-bfa0-c9bf74e6e06f](https://github.com/user-attachments/assets/bb3a2267-fd53-443d-bfa0-c9bf74e6e06f)

Make sure your UDP port is set to **21234**. Some settings (like DMX universe/start address) can interfere with DDP—set those carefully if you're tweaking sync interfaces.
Here’s what your sync settings should roughly look like:


<p align="center">
  <img src="https://github.com/user-attachments/assets/74615a0b-a21b-4938-a5e4-9d74cbd9f0e8"/>
</p>

---

## What your setup could look like (Maybe with a better color scheme)
https://github.com/user-attachments/assets/07af3149-8fe5-49e5-be16-c91e460b07d0


## 💻 Supported Platforms

LucaLights *theoretically* runs on **Windows**, **Linux**, and **macOS** — and it does work on all three!
That said, Windows is currently the primary supported platform, as a cross-platform build system is still in progress.

---

## 🔄 Updating

LucaLights includes an **automatic update checker**.
Future versions will let you **opt out of updates** if you prefer to stick with a specific release.

---

Let me know if you'd like to add installation instructions, build instructions, or badges (like version, license, etc.) for more polish.
