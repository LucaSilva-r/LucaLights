<h1 align="center">
  <img src="./Assets/logo.png" width=50/ align="center">
  LucaLights
</h1>
<h3 align="center">

Bring some color to your DDR sessions!

## What is this project?

**LucaLights** tries to lower the cost and complexity of lighting setups in StepMania and it's forks (Like ITGMania). **How?** by using **WLED** of course! By using wled (through the DDP protocol) you can use whatever LED Bulb and Light Strip you already have and adding more is as easy as buying one of the many controllers already available.

## Basic Setup Demo

https://github.com/user-attachments/assets/bb3a2267-fd53-443d-bfa0-c9bf74e6e06f

You just need the ip address of your WLED light and you are ready to go. Be careful when setting up your leds, as of now the UDP port must be 21234 and even though they shouldn't affect it, make sure these settings are setup like this in your sync interfaces. In my case changing dmx universe/start address messed with DDP and the light would not respond anymore
![image](https://github.com/user-attachments/assets/74615a0b-a21b-4938-a5e4-9d74cbd9f0e8)


## Supported Platforms

The project is in theory capable of running on Windows, Linux and Macos. (it does work) But i haven't quite figured out the correct way to setup a sensible build system for them.
For now Windows will be the main supported platform.

## Updating

The app automatically finds updates, in a later revision I'll add the option to ignore new updates.


