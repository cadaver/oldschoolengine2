# OldschoolEngine 2

Minimal line-based Commodore 64 emulator running on Unity that emulates just enough to run the recent Covert Bitops C64 games (Hessian, Steel Ranger...)

The original "oldschoolengine" ran on GameBoy Advance to run Metal Warrior 4, and it used a custom API for graphics, sound and file access. In contrast,
this project emulates a limited subset of an actual C64, so that the game can run unmodified.

Features:

- CPU emulation based on EMU6502 code by Yve Verstrepen
- Parts of SID emulation (noise, filter) based on jsSID by Mihaly Horvath
- Line-based VIC-II rendering
- Raster interrupt + partial CIA1 Timer A emulation
- Joystick port 2 control with arrows + ctrl as fire button
- Keyboard input of most C64 keys
- Virtual joystick on touch enabled devices (left side = direction, right side = fire)
- D64 & D81 image support, loading / saving via minimal (and incorrect) Kernal routine traps
- Save file persistence

Licensed under the MIT license, see the code for details. Use at own risk.

