﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace 鹰眼OCR.Audio
{
    class WavInfo
    {
        public bool IsWavFile { get; set; }
        public int Channel { get; set; }
        public int Rate { get; set; }
        public int Len { get; set; }

        public WavInfo(string fileName)
        {
            if (!File.Exists(fileName))
                throw new Exception("文件不存在！");

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    br.ReadBytes(8);
                    // 9-12个四个字节（wave标志）
                    string flag = new string(br.ReadChars(4));
                    IsWavFile = flag.ToLower() == "wave";
                    br.ReadBytes(10);
                    // 23-24两个字节（声道数）
                    Channel = br.ReadInt16();
                    // 25-28四个字节（采样率）
                    Rate = br.ReadInt32();
                    br.ReadBytes(6);
                    int bit = br.ReadInt16();// 采样位数
                    Len = GetWavLen(fs.Length / 1024.0, Channel, Rate, bit);
                }
            }
        }

        private int GetWavLen(double size, int channel, int rate, int bit)
        {
            double len = size * 8.0 / (rate / 1000.0 * bit * channel);
            return (int)(len + 0.5);
        }
    }
}