// MIT License
// 
// Copyright (c) 2018-2019 Lasse Oorni
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

public class VIC2 {

    public const int NUM_LINES = 312;
    public const int FIRST_VISIBLE_LINE = 50;
    public const int FIRST_INVISIBLE_LINE = 250;
    public const int CYCLES_PER_LINE = 63;
    static public readonly byte[] bitValues = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };

    // Pepto palette
    readonly Color[] _palette = {
        new Color(0, 0, 0),
        new Color(1, 1, 1),
        new Color(104f/255f, 55f/255f, 43f/255f),
        new Color(112f/255f, 164f/255f, 178f/255f),
        new Color(111f/255f, 61f/255f, 134f/255f),
        new Color(88f/255f, 141f/255f, 67f/255f),
        new Color(53f/255f, 40f/255f, 121f/255f),
        new Color(184f/255f, 199f/255f, 111f/255f),
        new Color(111f/255f, 79f/255f, 37f/255f),
        new Color(67f/255f, 57f/255f, 0),
        new Color(153f/255f, 103f/255f, 89f/255f),
        new Color(68f/255f, 68f/255f, 68f/255f),
        new Color(108f/255f, 108f/255f, 108f/255f),
        new Color(154f/255f, 210f/255f, 132f/255f),
        new Color(108f/255f, 94f/255f, 181f/255f),
        new Color(149f/255f, 149f/255f, 149f/255f)
    };

    Texture2D _screenTexture;
    Texture2D ScreenTexture { get { return _screenTexture; } }
    RAM64K _ram;
    Color[] _pixels;

    byte[] _lineChars = new byte[40];
    byte[] _lineColors = new byte[40];
    int _lineNum;
    int _nextBadlineLineNum;
    int _currentCharRow;
    int _charRow;
    int _bitmapRow;
    bool _idleState;
    bool[] _spriteActive = { false, false, false, false, false, false, false, false };
    byte[] _spriteRow = { 0, 0, 0, 0, 0, 0, 0, 0 };

    public VIC2(Texture2D screenTexture, RAM64K ram)
    {
        _screenTexture = screenTexture;
        _ram = ram;
        _pixels = new Color[320 * 200];

        for (int i = 0; i < 320 * 200; ++i)
            _pixels[i] = _palette[0];

        UpdateTexture();
    }

    public void UpdateTexture()
    {
        _screenTexture.SetPixels(_pixels);
        _screenTexture.Apply();
    }

    public void BeginFrame()
    {
        // Should be called just before visible line
        _lineNum = 0;
        _nextBadlineLineNum = 0;
        _charRow = 0;
        _currentCharRow = 0;
        _bitmapRow = 0;
        _idleState = true;
        for (int i = 0; i < 8; ++i)
            _spriteActive[i] = false;
    }

    private void DoBadLine(int yScroll)
    {
        _currentCharRow = _charRow;

        if (_charRow >= 25)
        {
            _idleState = true;
            return;
        }

        _idleState = false;
        _bitmapRow = _charRow;
        _nextBadlineLineNum = (_charRow + 1) * 8 + yScroll - 3;
        ++_charRow;
    }

    public void RenderNextLine()
    {
        if (_lineNum >= 200)
            return;

        int pixelStart = _lineNum * 320;
        Color black = _palette[0];
        Color bgColor = _palette[_ram.ReadIO(0xd021) & 0xf];
        Color borderColor = _palette[_ram.ReadIO(0xd020) & 0xf];

        byte control = _ram.ReadIO(0xd011);
        int yScroll = control & 0x7;
        int xScroll = _ram.ReadIO(0xd016) & 0x7;
        bool hBorders = (_ram.ReadIO(0xd016) & 0x8) == 0;
        bool vBorders = (control & 0x8) == 0;
        bool displayEnable = (control & 0x10) != 0;
        bool bitmapMode = (control & 0x20) != 0;
        bool multiColor = (_ram.ReadIO(0xd016) & 0x10) != 0;
        bool ebcMode = (control & 0x40) != 0;

        ushort videoBank = (ushort)(0xc000 - (_ram.ReadIO(0xdd00) & 0x3) * 0x4000);
        ushort charData = (ushort)(videoBank + (_ram.ReadIO(0xd018) & 0xe) * 0x400);
        ushort bitmapData = (ushort)(videoBank + (_ram.ReadIO(0xd018) & 0x8) * 0x400);
        ushort screenAddress = (ushort)(videoBank + (_ram.ReadIO(0xd018) & 0xf0) * 0x40);

        Color mc1 = _palette[_ram.ReadIO(0xd022) & 0xf];
        Color mc2 = _palette[_ram.ReadIO(0xd023) & 0xf];
        Color mc3 = _palette[_ram.ReadIO(0xd024) & 0xf];

        if ((_lineNum == 0 && ((_lineNum + 3) & 0x7) >= yScroll) || (((_lineNum + 3) & 0x7) == yScroll && _lineNum >= _nextBadlineLineNum))
            DoBadLine(yScroll);

        // HACK for Hessian scrolling: actually get chars & colors every line
        if (!_idleState)
        {
            for (int i = 0; i < 40; ++i)
            {
                _lineChars[i] = _ram.ReadRAM((ushort)(screenAddress + _currentCharRow * 40 + i));
                _lineColors[i] = (byte)(_ram.ReadIO((ushort)(0xd800 + _currentCharRow * 40 + i)) & 0xf);
            }
        }

        int charIndex = 0;
        int charRow = (_lineNum + 3 - yScroll) & 0x7;
        int bit = 0x80 << xScroll;

        bool renderSprites = true;

        // V-border or display off
        if (!displayEnable || (vBorders && (_lineNum < 4 || _lineNum >= 196)))
        {
            for (int i = 0; i < 320; ++i)
                _pixels[pixelStart + i] = borderColor;
            renderSprites = false;
        }
        else
        // Idle state or illegal mode (render just black)
        if (_idleState || (ebcMode && multiColor))
        {
            for (int i = 0; i < 320; ++i)
                _pixels[pixelStart + i] = black;
        }
        // Charmode
        else if (!bitmapMode)
        {
            byte charByte = ebcMode ? _ram.ReadRAM((ushort)(charData + (_lineChars[charIndex] & 0x3f) * 8 + charRow)) :
                _ram.ReadRAM((ushort)(charData + _lineChars[charIndex] * 8 + charRow));

            // Singlecolor
            if (!multiColor)
            {
                for (int i = 0; i < 320; ++i)
                {
                    if (hBorders && (i < 7 || i >= 311))
                        _pixels[pixelStart + i] = borderColor;
                    else
                    {
                        if (bit > 0x80 || charByte == 0 || (charByte & bit) == 0)
                        {
                            if (!ebcMode)
                                _pixels[pixelStart + i] = bgColor;
                            else
                            {
                                switch (_lineChars[charIndex] >> 6)
                                {
                                    case 0:
                                        _pixels[pixelStart + i] = bgColor;
                                        break;
                                    case 1:
                                        _pixels[pixelStart + i] = mc1;
                                        break;
                                    case 2:
                                        _pixels[pixelStart + i] = mc2;
                                        break;
                                    case 3:
                                        _pixels[pixelStart + i] = mc3;
                                        break;
                                }
                            }
                        }
                        else
                            _pixels[pixelStart + i] = _palette[_lineColors[charIndex]];
                    }

                    bit >>= 1;
                    if (bit == 0)
                    {
                        bit = 0x80;
                        ++charIndex;
                        if (charIndex < 40)
                        {
                            charByte = ebcMode ? _ram.ReadRAM((ushort)(charData + (_lineChars[charIndex] & 0x3f) * 8 + charRow)) :
                                _ram.ReadRAM((ushort)(charData + _lineChars[charIndex] * 8 + charRow));
                        }
                    }
                }
            }
            // Multicolor
            else
            {
                int bitPairShift = 0x7 + xScroll;

                for (int i = 0; i < 320; ++i)
                {
                    if (hBorders && (i < 7 || i >= 311))
                        _pixels[pixelStart + i] = borderColor;
                    else
                    {
                        if (bit > 0x80 || charByte == 0)
                            _pixels[pixelStart + i] = bgColor;
                        else
                        {
                            if (_lineColors[charIndex] < 0x8)
                            {
                                if ((charByte & bit) != 0)
                                    _pixels[pixelStart + i] = _palette[_lineColors[charIndex]];
                                else
                                    _pixels[pixelStart + i] = bgColor;
                            }
                            else
                            {
                                byte bitPair = (byte)((charByte >> (bitPairShift & 0x6)) & 0x3);
                                switch (bitPair)
                                {
                                    case 0:
                                        _pixels[pixelStart + i] = bgColor;
                                        break;
                                    case 1:
                                        _pixels[pixelStart + i] = mc1;
                                        break;
                                    case 2:
                                        _pixels[pixelStart + i] = mc2;
                                        break;
                                    case 3:
                                        _pixels[pixelStart + i] = _palette[_lineColors[charIndex] & 0x7];
                                        break;
                                }
                            }
                        }
                    }

                    bit >>= 1;
                    --bitPairShift;
                    if (bit == 0)
                    {
                        bit = 0x80;
                        bitPairShift = 0x7;
                        ++charIndex;
                        if (charIndex < 40)
                            charByte = _ram.ReadRAM((ushort)(charData + _lineChars[charIndex] * 8 + charRow));
                    }
                }
            }
        }
        else if (bitmapMode)
        {
            byte charByte = _ram.ReadRAM((ushort)(bitmapData + _bitmapRow * 320 + charIndex * 8 + charRow));

            // Singlecolor
            if (!multiColor)
            {
                for (int i = 0; i < 320; ++i)
                {
                    if (hBorders && (i < 7 || i >= 311))
                        _pixels[pixelStart + i] = borderColor;
                    else
                    {
                        if (bit > 0x80 || charByte == 0)
                            _pixels[pixelStart + i] = bgColor;
                        else
                        {
                            if ((charByte & bit) != 0)
                                _pixels[pixelStart + i] = _palette[_lineChars[charIndex] >> 4];
                            else
                                _pixels[pixelStart + i] = _palette[_lineChars[charIndex] & 0xf];
                        }
                    }

                    bit >>= 1;
                    if (bit == 0)
                    {
                        bit = 0x80;
                        ++charIndex;
                        if (charIndex < 40)
                            charByte = _ram.ReadRAM((ushort)(bitmapData + _bitmapRow * 320 + charIndex * 8 + charRow));
                    }
                }
            }
            // Multicolor
            else
            {
                int bitPairShift = 0x7 + xScroll;

                for (int i = 0; i < 320; ++i)
                {
                    if (hBorders && (i < 7 || i >= 311))
                        _pixels[pixelStart + i] = borderColor;
                    else
                    {
                        if (bit > 0x80 || charByte == 0)
                            _pixels[pixelStart + i] = bgColor;
                        else
                        {
                            byte bitPair = (byte)((charByte >> (bitPairShift & 0x6)) & 0x3);
                            switch (bitPair)
                            {
                                case 0:
                                    _pixels[pixelStart + i] = bgColor;
                                    break;
                                case 1:
                                    _pixels[pixelStart + i] = _palette[_lineChars[charIndex] >> 4];
                                    break;
                                case 2:
                                    _pixels[pixelStart + i] = _palette[_lineChars[charIndex] & 0xf];
                                    break;
                                case 3:
                                    _pixels[pixelStart + i] = _palette[_lineColors[charIndex] & 0xf];
                                    break;
                            }
                        }
                    }

                    bit >>= 1;
                    --bitPairShift;
                    if (bit == 0)
                    {
                        bit = 0x80;
                        bitPairShift = 0x7;
                        ++charIndex;
                        if (charIndex < 40)
                            charByte = _ram.ReadRAM((ushort)(bitmapData + _bitmapRow * 320 + charIndex * 8 + charRow));
                    }
                }
            }
        }

        byte spriteFlags = _ram.ReadIO(0xd015);
        byte spriteMCFlags = _ram.ReadIO(0xd01c);
        byte spriteXMSBFlags = _ram.ReadIO(0xd010);
        byte spriteXExpandFlags = _ram.ReadIO(0xd01d);

        Color sprMc1 = _palette[_ram.ReadIO(0xd025) & 0xf];
        Color sprMc2 = _palette[_ram.ReadIO(0xd026) & 0xf];

        for (int i = 7; i >= 0; --i)
        {
            byte spriteY = _ram.ReadIO((ushort)(0xd001 + i * 2));
            if (!_spriteActive[i] && (spriteFlags & bitValues[i]) != 0)
            {
                if (_lineNum == spriteY - 50 || (_lineNum == 0 && spriteY >= 30 && spriteY < 50))
                {
                    _spriteActive[i] = true;
                    _spriteRow[i] = (byte)(_lineNum + 50 - spriteY);
                }
            }

            if (_spriteActive[i])
            {
                // TODO: Y expansion, background priority
                if (renderSprites)
                {
                    int startX = _ram.ReadIO((ushort)(0xd000 + i * 2));
                    if ((spriteXMSBFlags & bitValues[i]) != 0)
                        startX += 256;
                    bool xExpand = (spriteXExpandFlags & bitValues[i]) != 0;
                    if (xExpand && startX >= 480 && startX < 504)
                        startX -= 504;

                    ushort spriteData = (ushort)(videoBank + _ram.ReadRAM((ushort)(screenAddress + 0x3f8 + i)) * 0x40 + _spriteRow[i] * 3);
                    Color spriteColor = _palette[_ram.ReadIO((ushort)(0xd027 + i)) & 0xf];

                    for (int j = 0; j < 24; ++j)
                    {
                        int k = xExpand ? (startX + j * 2 - 24) : (startX + j - 24);
                        byte spriteByte = _ram.ReadRAM((ushort)(spriteData + (j >> 3)));

                        for (int l = 0; l < (xExpand ? 2 : 1); ++l)
                        {
                            if (k >= 0 && k <= 320 && (!hBorders || (k >= 7 && k < 311)) && spriteByte != 0)
                            {
                                if ((spriteMCFlags & bitValues[i]) != 0)
                                {
                                    byte bitPair = (byte)((spriteByte >> (6 - (j & 0x6))) & 0x3);
                                    switch (bitPair)
                                    {
                                        case 1:
                                            _pixels[pixelStart + k] = sprMc1;
                                            break;
                                        case 2:
                                            _pixels[pixelStart + k] = spriteColor;
                                            break;
                                        case 3:
                                            _pixels[pixelStart + k] = sprMc2;
                                            break;
                                    }
                                }
                                else
                                {
                                    if ((spriteByte & bitValues[7 - (j & 0x7)]) != 0)
                                        _pixels[pixelStart + k] = spriteColor;
                                }
                            }
                            ++k;
                        }
                    }
                }

                ++_spriteRow[i];
                if (_spriteRow[i] >= 21)
                    _spriteActive[i] = false;
            }
        }

        // Done, increment linecount
        ++_lineNum;
    }
}
