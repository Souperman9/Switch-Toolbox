﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Toolbox.Library.IO;

namespace LayoutBXLYT.Cafe
{
    public class LYT1 : LayoutInfo
    {
        [DisplayName("Max Parts Width"), CategoryAttribute("Layout")]
        public float MaxPartsWidth { get; set; }

        [DisplayName("Max Parts Height"), CategoryAttribute("Layout")]
        public float MaxPartsHeight { get; set; }

        public LYT1()
        {
            DrawFromCenter = false;
            Width = 0;
            Height = 0;
            MaxPartsWidth = 0;
            MaxPartsHeight = 0;
            Name = "";
        }

        public LYT1(FileReader reader)
        {
            DrawFromCenter = reader.ReadBoolean();
            reader.Seek(3); //padding
            Width = reader.ReadSingle();
            Height = reader.ReadSingle();
            MaxPartsWidth = reader.ReadSingle();
            MaxPartsHeight = reader.ReadSingle();
            Name = reader.ReadZeroTerminatedString();
        }

        public override void Write(FileWriter writer, LayoutHeader header)
        {
            writer.Write(DrawFromCenter);
            writer.Seek(3);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(MaxPartsWidth);
            writer.Write(MaxPartsHeight);
            writer.WriteString(Name);
        }
    }
}
