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

public struct KeyMapping
{
    public KeyCode keyCode;
    public byte matrixSlot;
    public string axis;

    public KeyMapping(KeyCode code, byte slot, string axisName = "")
    {
        keyCode = code;
        matrixSlot = slot;
        axis = axisName;
    }
}

public class Controls : MonoBehaviour {

    public SpriteRenderer joystickCenter;
    public SpriteRenderer joystickBase;
    public SpriteRenderer fireButton;

    internal byte joystick;
    internal byte[] keyMatrix = new byte[8];

    static readonly KeyMapping[] keyMappings = {
        new KeyMapping(KeyCode.Backspace, 0),
        new KeyMapping(KeyCode.Return, 1),
        new KeyMapping(KeyCode.F7, 3),
        new KeyMapping(KeyCode.F1, 4),
        new KeyMapping(KeyCode.F3, 5),
        new KeyMapping(KeyCode.F5, 6),
        new KeyMapping(KeyCode.Alpha3, 8),
        new KeyMapping(KeyCode.W, 9),
        new KeyMapping(KeyCode.A, 10),
        new KeyMapping(KeyCode.Alpha4, 11),
        new KeyMapping(KeyCode.Z, 12),
        new KeyMapping(KeyCode.S, 13),
        new KeyMapping(KeyCode.E, 14),
        new KeyMapping(KeyCode.LeftShift, 15),
        new KeyMapping(KeyCode.Alpha5, 16),
        new KeyMapping(KeyCode.R, 17),
        new KeyMapping(KeyCode.D, 18),
        new KeyMapping(KeyCode.Alpha6, 19),
        new KeyMapping(KeyCode.C, 20),
        new KeyMapping(KeyCode.F, 21),
        new KeyMapping(KeyCode.T, 22),
        new KeyMapping(KeyCode.X, 23),
        new KeyMapping(KeyCode.Alpha7, 24),
        new KeyMapping(KeyCode.Y, 25),
        new KeyMapping(KeyCode.G, 26),
        new KeyMapping(KeyCode.Alpha8, 27),
        new KeyMapping(KeyCode.B, 28),
        new KeyMapping(KeyCode.H, 29),
        new KeyMapping(KeyCode.U, 30),
        new KeyMapping(KeyCode.V, 31),
        new KeyMapping(KeyCode.Alpha9, 32),
        new KeyMapping(KeyCode.I, 33),
        new KeyMapping(KeyCode.J, 34),
        new KeyMapping(KeyCode.Alpha0, 35),
        new KeyMapping(KeyCode.M, 36),
        new KeyMapping(KeyCode.K, 37),
        new KeyMapping(KeyCode.O, 38),
        new KeyMapping(KeyCode.N, 39),
        new KeyMapping(KeyCode.Plus, 40),
        new KeyMapping(KeyCode.P, 41),
        new KeyMapping(KeyCode.L, 42),
        new KeyMapping(KeyCode.Minus, 43),
        new KeyMapping(KeyCode.Period, 44, "SelectR"),
        new KeyMapping(KeyCode.Colon, 45),
        new KeyMapping(KeyCode.At, 46),
        new KeyMapping(KeyCode.Comma, 47, "SelectL"),
        new KeyMapping(KeyCode.Asterisk, 49),
        new KeyMapping(KeyCode.Semicolon, 50),
        new KeyMapping(KeyCode.Home, 51),
        new KeyMapping(KeyCode.RightShift, 52),
        new KeyMapping(KeyCode.Equals, 53),
        new KeyMapping(KeyCode.Slash, 55),
        new KeyMapping(KeyCode.Alpha1, 56),
        new KeyMapping(KeyCode.Alpha2, 59),
        new KeyMapping(KeyCode.Space, 60),
        new KeyMapping(KeyCode.Q, 62),
        new KeyMapping(KeyCode.Escape, 63, "Start")
    };

    int _joystickFingerId = -1;
    Vector2 _joystickFingerStartPos;

    public void UpdateJoystick()
    {
        joystick = 0x0;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool fire = Input.GetButton("Fire1");
        if (v > 0f) joystick |= 0x1;
        if (v < 0f) joystick |= 0x2;
        if (h < 0f) joystick |= 0x4;
        if (h > 0f) joystick |= 0x8;
        if (fire) joystick |= 0x10;

        bool hasJoystickTouch = false;
        fireButton.gameObject.SetActive(false);

        foreach (Touch touch in Input.touches)
        {
            if (touch.phase != TouchPhase.Ended || touch.phase != TouchPhase.Canceled)
            {
                if (touch.position.x >= Screen.width / 2)
                {
                    joystick |= 0x10;
                    fireButton.gameObject.SetActive(true);
                    if (touch.phase == TouchPhase.Began)
                        fireButton.transform.position = Camera.main.ScreenToWorldPoint(touch.position);
                }
                else
                {
                    if (_joystickFingerId < 0)
                    {
                        _joystickFingerId = touch.fingerId;
                        _joystickFingerStartPos = touch.position;
                        joystickBase.gameObject.SetActive(true);
                        joystickBase.transform.position = Camera.main.ScreenToWorldPoint(touch.position);
                    }

                    if (touch.fingerId == _joystickFingerId)
                    {
                        hasJoystickTouch = true;
                        Vector2 delta = touch.position - _joystickFingerStartPos;
                        delta.x /= Screen.height;
                        delta.y /= Screen.height;
                        const float threshold = 0.04f;
                        delta.x = Mathf.Clamp(delta.x, -threshold, threshold);
                        delta.y = Mathf.Clamp(delta.y, -threshold, threshold);
                        joystickCenter.transform.localPosition = delta * 2f;

                        if (delta.y >= threshold)
                            joystick |= 0x1;
                        if (delta.y <= -threshold)
                            joystick |= 0x2;
                        if (delta.x <= -threshold)
                            joystick |= 0x4;
                        if (delta.x >= threshold)
                            joystick |= 0x8;
                    }
                }
            }
        }

        if (!hasJoystickTouch)
        {
            _joystickFingerId = -1;
            joystickBase.gameObject.SetActive(false);
        }

        joystick ^= 0xff;
    }

    public void UpdateKeyboard()
    {
        for (int i = 0; i < 8; ++i)
            keyMatrix[i] = 0xff;

        foreach (KeyMapping mapping in keyMappings)
        {
            if (Input.GetKey(mapping.keyCode) || mapping.axis.Length > 0 && Input.GetAxis(mapping.axis) > 0f)
                keyMatrix[mapping.matrixSlot >> 3] &= (byte)(~VIC2.bitValues[mapping.matrixSlot & 0x7]);
        }
    }

}
