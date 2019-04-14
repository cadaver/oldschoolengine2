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
using System.IO;
using UnityEngine;

public class FileHandle
{
    public int track;
    public int sector;
    public int offset;
    public System.IO.BinaryReader reader;
    public System.IO.BinaryWriter writer;

    public bool Open { get { return reader != null || writer != null || track != 0; } }
    public void Close()
    {
        if (reader != null)
        {
            reader.Close();
            reader = null;
        }
        if (writer != null)
        {
            writer.Close();
            writer = null;
        }
    }
}

public class DiskImage {

    public enum DiskType
    {
        D64 = 0,
        D81
    }

    public const int MAX_D64_TRACK = 35;
    public const int MAX_D64_SECTOR = 21;
    public const int MAX_TRACK = 80;
    public const int MAX_SECTOR = 40;

    static public readonly int[] d64SectorsPerTrack =
    {
        0, 21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,21,
        19,19,19,19,19,19,19,
        18,18,18,18,18,18,
        17,17,17,17,17
    };

    string _name;
    DiskType _type;

    int[,] _sectorOffsets = new int[MAX_TRACK + 1, MAX_SECTOR];
    byte[] _data;

    public DiskImage(string name)
    {
        TextAsset diskAsset = Resources.Load(name) as TextAsset;
        if (diskAsset != null)
        {
            _name = name;
            _data = diskAsset.bytes;
            _type = _data.Length == 819200 ? DiskType.D81 : DiskType.D64;
        }
        else
        {
            Debug.LogError("Failed to open disk image " + name);
            _data = new byte[174848];
            _type = DiskType.D64;
        }
        
        MakeSectorTable();
    }

    public int GetSectorOffset(int track, int sector)
    {
        return _sectorOffsets[track, sector];
    }

    public FileHandle OpenFileForWrite(byte[] fileName)
    {
        try
        {
            BinaryWriter writer = new BinaryWriter(File.Open(GetSaveFileName(fileName), FileMode.Create));
            if (writer != null)
            {
                FileHandle handle = new FileHandle();
                handle.writer = writer;
                return handle;
            }
        }
        catch (System.Exception)
        {
        }

        return null;
    }

    public FileHandle OpenFile(byte[] fileName)
    {
        // Check for saved file
        try
        {
            BinaryReader reader = new BinaryReader(File.Open(GetSaveFileName(fileName), FileMode.Open));
            if (reader != null)
            {
                FileHandle handle = new FileHandle();
                handle.reader = reader;
                return handle;
            }
        }
        catch (System.Exception)
        {
        }

        int dirTrack = (_type == DiskType.D64) ? 18 : 40;
        int dirSector = (_type == DiskType.D64) ? 1 : 3;

        while (dirTrack > 0)
        {
            int offset = GetSectorOffset(dirTrack, dirSector);
            for (int d = 2; d < 256; d += 32)
            {
                if (_data[offset + d] == 0x82)
                {
                    bool match = true;

                    // If no filename specified, open any file
                    if (fileName != null)
                    {
                        for (int e = 0; e < fileName.Length; ++e)
                        {
                            if (_data[offset + d + 3 + e] != fileName[e])
                            {
                                match = false;
                                break;
                            }
                        }
                    }

                    if (match)
                    {
                        FileHandle ret = new FileHandle();
                        ret.track = _data[offset + d + 1];
                        ret.sector = _data[offset + d + 2];
                        ret.offset = 2;
                        return ret;
                    }
                }
            }

            // Next sector
            dirTrack = _data[offset];
            dirSector = _data[offset + 1];
        }

        return null;
    }

    public byte ReadByte(FileHandle handle)
    {
        if (!handle.Open)
            return 0;

        byte ret;

        if (handle.reader != null)
        {
            ret = handle.reader.ReadByte();
            if (handle.reader.BaseStream.Position >= handle.reader.BaseStream.Length)
            {
                handle.Close();
            }
            return ret;
        }

        int sectorStart = GetSectorOffset(handle.track, handle.sector);
        ret = _data[sectorStart + handle.offset];

        // Last sector?
        if (_data[sectorStart] == 0)
        {
            // Last byte read?
            if (handle.offset >= _data[sectorStart + 1])
                handle.track = 0;
            else
                ++handle.offset;
        }
        else
        {
            ++handle.offset;
            if (handle.offset >= 256)
            {
                handle.track = _data[sectorStart];
                handle.sector = _data[sectorStart + 1];
                handle.offset = 2;
            }
        }

        return ret;
    }

    public void WriteByte(FileHandle handle, byte value)
    {
        if (handle.writer != null)
            handle.writer.Write(value);
    }

    string GetSaveFileName(byte[] fileName)
    {
        return Application.persistentDataPath + "/" + _name + System.Text.Encoding.ASCII.GetString(fileName);
    }

    void MakeSectorTable()
    {
        int offset = 0;

        if (_type == DiskType.D64)
        {
            for (int c = 1; c <= MAX_D64_TRACK; ++c)
            {
                for (int d = 0; d < d64SectorsPerTrack[c]; ++d)
                {
                    _sectorOffsets[c, d] = offset;
                    offset += 256;
                }
            }
        }
        else
        {
            for (int c = 1; c <= MAX_TRACK; ++c)
            {
                for (int d = 0; d < MAX_SECTOR; ++d)
                {
                    _sectorOffsets[c, d] = offset;
                    offset += 256;
                }
            }
        }
    }

}
