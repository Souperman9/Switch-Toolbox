using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.Forms;
using Syroot.BinaryData;
using Toolbox.Library.IO;

namespace FirstPlugin
{
    public class TRB : IArchiveFile, IFileFormat
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "de Blob 2 Archive (TRB)" };
        public string[] Extension { get; set; } = new string[] { "*.trb" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return reader.CheckSignature(3, "TRB") || Utils.HasExtension(FileName, ".trb");
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                return types.ToArray();
            }
        }

        public bool CanAddFiles => throw new NotImplementedException();

        public bool CanRenameFiles => throw new NotImplementedException();

        public bool CanReplaceFiles => throw new NotImplementedException();

        public bool CanDeleteFiles => throw new NotImplementedException();
        
        public List<FileEntry> files = new List<FileEntry>();
        public IEnumerable<ArchiveFileInfo> Files => files;

        public void Load(System.IO.Stream stream)
        {
            using (var reader = new FileReader(stream))
            {
                files.Clear();
                reader.ByteOrder = ByteOrder.LittleEndian;
                TRB.Header header = new TRB.Header();
                header.version = reader.ReadUInt32(4);
                reader.Position += 4;
                header.flag1 = reader.ReadUInt16();
                header.flag2 = reader.ReadUInt16();
                header.dataInfoCount = reader.ReadUInt32();
                header.dataInfoSize = reader.ReadUInt32();
                header.tagCount = reader.ReadUInt32();
                header.tagSize = reader.ReadUInt32();
                header.relocationDataOffset = reader.ReadUInt32();
                header.relocationDataSize = reader.ReadInt32();
                reader.Position += 92;
                Console.WriteLine(header.magic);
                DataInfo[] dataInfos = new DataInfo[header.dataInfoCount];
                TagInfo[] tagInfos = new TagInfo[header.tagCount];

                for (int i = 0; i < header.dataInfoCount; i++)
                {
                    
                    dataInfos[i] = new DataInfo()
                    {
                        unknown1 = reader.ReadUInt32(),
                        textOffset = reader.ReadUInt32(),
                        unknown2 = reader.ReadUInt32(),
                        unknown3 = reader.ReadUInt32(),
                        dataSize = reader.ReadInt32(),
                        dataSize2 = reader.ReadUInt32(),
                        dataOffset = reader.ReadUInt32(),
                        unknown4 = reader.ReadUInt32(),
                        zero1 = reader.ReadUInt32(),
                        zero2 = reader.ReadUInt32(),
                        zero3 = reader.ReadUInt32(),
                        zero4 = reader.ReadUInt32()
                    };
                    Console.WriteLine(dataInfos[i].dataOffset);
                }
                
                for (int i = 0; i < header.tagCount; i++)
                {

                    tagInfos[i] = new TagInfo()
                    {
                        magic = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)),
                        dataOffset = reader.ReadUInt32(),
                        flag = reader.ReadUInt32(),
                        textOffset = reader.ReadInt32()
                    };
                    Console.WriteLine(tagInfos[i].dataOffset);
                }
                reader.Position = dataInfos[0].dataOffset;
                //byte[] textData = reader.ReadBytes(dataInfos[0].dataSize);
                reader.Position = dataInfos[1].dataOffset;
                byte[] rawData = reader.ReadBytes(dataInfos[1].dataSize);
                if (header.dataInfoCount > 2)
                {
                    reader.Position = dataInfos[2].dataOffset;
                    byte[] extraData = reader.ReadBytes(dataInfos[2].dataSize);
                }
                reader.Position = header.relocationDataOffset;
                byte[] relocationData = reader.ReadBytes(header.relocationDataSize);
                Console.WriteLine(reader.Position);
                for (int i = 0; i < header.tagCount; i++)
                {
                    reader.Position = dataInfos[0].dataOffset + tagInfos[i].textOffset;
                    string filename = reader.ReadZeroTerminatedString() + "." + tagInfos[i].magic.ToLower();
                    Console.WriteLine(filename);
                }
            }
        }
        public void Unload() //This is used when the file format is disposed of
        {

        }
        public byte[] Save() //Returns a file to save. Note this is without compression as that is done later!
        {
            return null;
        }

        public void ClearFiles()
        {
            throw new NotImplementedException();
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            throw new NotImplementedException();
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            throw new NotImplementedException();
        }

        public void Save(Stream stream)
        {
            throw new NotImplementedException();
        }
        
        public class FileEntry : ArchiveFileInfo
        {

        }

        class Header
        {
            public string magic = "TRB";
            public uint version;
            public UInt16 flag1;
            public UInt16 flag2;
            public uint dataInfoCount;
            public uint dataInfoSize;
            public uint tagCount;
            public uint tagSize;
            public uint relocationDataOffset;
            public int relocationDataSize;
        }
        public class DataInfo
        {
            public uint unknown1;
            public uint textOffset;
            public uint unknown2;
            public uint unknown3;
            public int dataSize;
            public uint dataSize2;
            public uint dataOffset;
            public uint unknown4;
            public uint zero1;
            public uint zero2;
            public uint zero3;
            public uint zero4;
        }

        public class TagInfo
        {
            public string magic;
            public uint dataOffset;
            public uint flag;
            public int textOffset;
        }

    }
}