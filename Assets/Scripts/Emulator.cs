// MIT License
// 
// Copyright (c) 2018-2024 Lasse Oorni
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EMU6502;

public class Emulator : MonoBehaviour 
{
    public const string diskImageName = "steelrangerdemo";

    public SpriteRenderer screenRect;
    public Controls controls;

    RAM64K _ram;
    MOS6502 _processor;
    VIC2 _vic2;
    SID _sid;

    int _lineCounter;
    int _timer;
    bool _timerIRQEnable;
    bool _timerIRQFlag;
    bool _rasterIRQFlag;

    Texture2D _screenTexture;
    bool _textureDirty;
    int _audioCycles;
    bool _underRun;

    DiskImage _disk;
    byte[] _fileName;
    FileHandle _fileHandle;

    float _lastSidSample = 0f;

    void Awake()
    {
        _screenTexture = new Texture2D(320, 200, TextureFormat.RGB24, false);
        _ram = new RAM64K();
        _ram.ioRead += HandleIORead;
        _ram.ioWrite += HandleIOWrite;
        _processor = new MOS6502(_ram);
        _processor.kernalTrap += HandleKernalTrap;
        _vic2 = new VIC2(_screenTexture, _ram);
        _sid = new SID(_ram);

        _screenTexture.wrapMode = TextureWrapMode.Clamp;
        screenRect.sprite = Sprite.Create(_screenTexture, new Rect(0, 0, _screenTexture.width, _screenTexture.height), new Vector2(0.5f, 0.5f));
        screenRect.material.SetTexture("_MainTex", _screenTexture);
        screenRect.transform.localScale = new Vector2(1f, -1f);

        InitMemory();
        BootGame();
    }

    void InitMemory()
    {
        _ram[0x01] = 0x37;
        _ram.WriteIO(0xd018, 0x14);
        _ram.WriteIO(0xd011, 27);
        _ram.WriteIO(0xd016, 24);
        _ram.WriteIO(0xdd00, 0x3);
        _ram.WriteIO(0xd030, 0xff);
        _ram.WriteIO(0xd0bc, 0xff);
        _ram.WriteIO(0xdc00, 0xff);
    }

    void BootGame()
    {
        _disk = new DiskImage(diskImageName);

        // No filename, open first file in directory
        FileHandle bootFile = _disk.OpenFile(null);
        if (bootFile != null)
        {
            ushort loadAddress = (ushort)(_disk.ReadByte(bootFile) + _disk.ReadByte(bootFile) * 256);
            ushort address = loadAddress;
            while (bootFile.Open)
                _ram[address++] = _disk.ReadByte(bootFile);
            // Set end address on zero page
            _ram[0x2d] = (byte)(address & 0xff);
            _ram[0x2e] = (byte)(address >> 8);
            _ram[0xae] = (byte)(address & 0xff);
            _ram[0xaf] = (byte)(address >> 8);
            // Set RESET vector to CLALL (autostart), otherwise assume sys2061
            if (loadAddress <= 0x32c)
            {
                _ram[0xfffc] = _ram[0x32c];
                _ram[0xfffd] = _ram[0x32d];
            }
            else
            {
                _ram[0xfffc] = 0xd;
                _ram[0xfffd] = 0x8;
            }
        }
    }

    void Update()
    {
        // Do here to not miss touches
        controls.UpdateJoystick();
        controls.UpdateKeyboard();

        if (_textureDirty)
        {
            _vic2.UpdateTexture();
            _textureDirty = false;
        }

        if (_underRun)
        {
            Debug.LogWarning("Audio underrun!");
            _underRun = false;
        }

        #if UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape)) 
            Application.Quit();
        #endif
    }

    void FixedUpdate()
    {
        const int frameCycles = VIC2.CYCLES_PER_LINE * VIC2.NUM_LINES;

        _processor.Cycles = 0;
        _audioCycles = 0;

        for (ushort i = 0; i < VIC2.FIRST_VISIBLE_LINE; ++i)
            ExecuteLine(i, false);

        _vic2.BeginFrame();

        for (ushort i = VIC2.FIRST_VISIBLE_LINE; i < VIC2.FIRST_INVISIBLE_LINE; ++i)
            ExecuteLine(i, true);

        for (ushort i = VIC2.FIRST_INVISIBLE_LINE; i < VIC2.NUM_LINES; ++i)
            ExecuteLine(i, false);

        // Render rest of audio until end of frame
        if (_audioCycles < frameCycles)
        {
            _sid.BufferSamples(frameCycles - _audioCycles);
            _audioCycles = frameCycles;
        }

        _textureDirty = true;

        //Debug.Log(_sid.samples.Count);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        lock (_sid.samplesLock)
        {
            int j = 0;
            for (int i = 0; i < data.Length; ++i)
            {
                if (j >= _sid.samples.Count)
                    _underRun = true;

                if (j < _sid.samples.Count && (i % channels == 0))
                    _lastSidSample = _sid.samples[j++];
                
                data[i] = _lastSidSample;
            }

            _sid.samples.RemoveRange(0, j);
        }
    }

    void ExecuteLine(ushort lineNum, bool visible)
    {
        UpdateLineCounterAndIRQ(lineNum);

        int targetCycles = VIC2.CYCLES_PER_LINE * lineNum;

        while (_processor.Cycles < targetCycles && !_processor.Jam)
        {
            _processor.SetIRQ(_timerIRQFlag || _rasterIRQFlag);
            _processor.Process();
        }

        if (visible)
            _vic2.RenderNextLine();
    }

    void UpdateLineCounterAndIRQ(int lineNum)
    {
        _lineCounter = lineNum;
        if ((_ram.ReadIO(0xd01a, false) & 0x1) > 0)
        {
            int targetLineNum = (_ram.ReadIO(0xd011, false) & 0x80) * 2 + _ram.ReadIO(0xd012, false);
            if (_lineCounter == targetLineNum)
                _rasterIRQFlag = true;
        }
        if (_timer > 0 && (_ram.ReadIO(0xdc0e, false) & 0x1) > 0)
        {
            _timer -= VIC2.CYCLES_PER_LINE;
            if (_timer <= 0)
            {
                _timer = 0;
                if (_timerIRQEnable)
                    _timerIRQFlag = true;
            }
        }
    }

    void HandleIOWrite(ushort address, byte value)
    {
        // Render audio up to the current point on each SID write
        if (address >= 0xd400 && address <= 0xd418)
        {
            _sid.BufferSamples(_processor.Cycles - _audioCycles);
            _audioCycles = _processor.Cycles;
        }
        else if (address == 0xdc0d)
        {
            if ((value & 0x81) == 0x81)
                _timerIRQEnable = true;
            if ((value & 0x81) == 0x1)
                _timerIRQEnable = false;
        }
        else if (address == 0xdc0e)
        {
            if ((value & 0x10) > 0)
                _timer = _ram.ReadIO(0xdc04, false) | (_ram.ReadIO(0xdc05, false) << 8);
        }
        else if (address == 0xd019)
        {
            // HACK: any write clears raster IRQ flag
            _rasterIRQFlag = false;
        }
    }

    byte HandleIORead(ushort address, out bool handled)
    {
        if (address == 0xdc00)
        {
            handled = true;
            return controls.joystick;
        }
        else if (address == 0xdc01)
        {
            handled = true;
            byte ret = 0xff;
            for (int i = 0; i < 8; ++i)
            {
                if ((_ram.ReadIO(0xdc00, false) & VIC2.bitValues[i]) == 0)
                    ret &= controls.keyMatrix[i];
            }
            return ret;
        }
        else if (address == 0xd011)
        {
            handled = true;
            return (byte)((byte)(_lineCounter >= 0x100 ? 0x80 : 0x00) | (_ram.ReadIO(0xd011, false) & 0x7f));
        }
        else if (address == 0xd012)
        {
            handled = true;
            return (byte)(_lineCounter & 0xff);
        }
        else if (address == 0xd01a)
        {
            handled = true;
            return (byte)(_ram.ReadIO(0xd01a, false) | 0xf0);
        }
        else if (address == 0xd030)
        {
            handled = true;
            return (byte)0xff;
        }
        else if (address == 0xdc0d)
        {
            handled = true;
            byte ret = 0x0;
            if (_timerIRQEnable)
                ret |= 0x1;
            if (_timerIRQFlag)
            {
                _timerIRQFlag = false;
                ret |= 0x80;
            }
            return ret;
        }
        else
        {
            handled = false;
            return 0;
        }
    }

    void HandleKernalTrap(ushort address)
    {
        // SETNAM
        if (address == 0xffbd)
        {
            //Debug.Log("SetNam");
            // Strip away @0:
            byte fileNameLength = _processor.A;
            ushort fileNameAddress = (ushort)(_processor.Y * 256 + _processor.X);
            if (_ram[fileNameAddress] == 0x40 && _ram[(ushort)(fileNameAddress + 1)] == 0x30)
            {
                fileNameAddress += 3;
                fileNameLength -= 3;
            }

            _fileName = new byte[fileNameLength];
            for (int i = 0; i < _fileName.Length; ++i)
                _fileName[i] = _ram[fileNameAddress++];
        }
        // CHKIN (actually open the file stream)
        else if (address == 0xffc6)
        {
            //Debug.Log("Chkin");
            _fileHandle = _disk.OpenFile(_fileName);
            if (_fileHandle == null)
            {
                Debug.LogWarning("File " + _fileName[0].ToString("X") + " " + _fileName[1].ToString("X") + " not found");
            }
        }
        // CHRIN
        else if (address == 0xffcf)
        {
            if (_fileHandle != null)
            {
                if (_fileHandle.Open)
                    _processor.A = _disk.ReadByte(_fileHandle);
                _ram[0x90] = (byte)(_fileHandle.Open ? 0x00 : 0x40);
            }
            else
            {
                _ram[0x90] = 0x42; // EOF, file not found
            }
        }
        // CHKOUT
        else if (address == 0xffc9)
        {
            _fileHandle = _disk.OpenFileForWrite(_fileName);
            if (_fileHandle == null)
            {
                Debug.LogError("File " + _fileName[0].ToString("X") + " " + _fileName[1].ToString("X") + " failed to open for write");
            }
        }
        // CHROUT
        else if (address == 0xffd2)
        {
            if (_fileHandle != null)
                _disk.WriteByte(_fileHandle, _processor.A);
        }
        // CLOSE
        else if (address == 0xffc3)
        {
            //Debug.Log("Close");
            if (_fileHandle != null)
                _fileHandle.Close();
            _fileHandle = null;
        }
        // CIOUT - loader detection
        else if (address == 0xffa8)
        {
            //Debug.Log("CIOut");
            _ram[0x90] = 0x80; // Error, prevent fastloader from running
        }
    }
}