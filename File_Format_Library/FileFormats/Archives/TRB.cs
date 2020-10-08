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
        public string[] Description { get; set; } = new string[] { "de Blob 2 Archive" };
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

        public bool CanRenameFiles { get; set; } = true;

        public bool CanReplaceFiles { get; set; } = false;

        public bool CanDeleteFiles => throw new NotImplementedException();
        
        public List<FileEntry> files = new List<FileEntry>();
        public IEnumerable<ArchiveFileInfo> Files => files;

        public void Load(System.IO.Stream stream)
        {
            using (var reader = new FileReader(stream))
            {
                // Read header for data locations
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
                // Create arrays for multiple data and tag info objects
                DataInfo[] dataInfos = new DataInfo[header.dataInfoCount];
                TagInfo[] tagInfos = new TagInfo[header.tagCount];

                for (int i = 0; i < header.dataInfoCount; i++)
                {
                    // Create and assign properties to a new DataInfo until the count specified in the header is reached
                    dataInfos[i] = new DataInfo()
                    {
                        unknown1 = reader.ReadUInt32(),
                        textOffset = reader.ReadUInt32(),
                        unknown2 = reader.ReadUInt32(),
                        unknown3 = reader.ReadUInt32(),
                        dataSize = reader.ReadInt32(),
                        dataSize2 = reader.ReadUInt32(),
                        dataOffset = reader.ReadInt32(),
                        unknown4 = reader.ReadUInt32(),
                        zero1 = reader.ReadUInt32(),
                        zero2 = reader.ReadUInt32(),
                        zero3 = reader.ReadUInt32(),
                        zero4 = reader.ReadUInt32()
                    };
                }
                
                for (int i = 0; i < header.tagCount; i++)
                {
                    // Get tags for file extensions, data, and names
                    tagInfos[i] = new TagInfo()
                    {
                        magic = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)),
                        dataOffset = reader.ReadInt32(),
                        flag = reader.ReadUInt32(),
                        textOffset = reader.ReadInt32()
                    };
                }

                // Get extra data, currently unused
                if (header.dataInfoCount > 2)
                {
                    reader.Position = dataInfos[2].dataOffset;
                    byte[] extraData = reader.ReadBytes(dataInfos[2].dataSize);
                }

                // Get relocation data and write to byte array
                reader.Position = header.relocationDataOffset;
                byte[] relocationData = reader.ReadBytes(header.relocationDataSize);

                // Compile filenames and add as files
                for (long i = 0; i < header.tagCount - 1; i++)
                {
                    reader.Position = dataInfos[0].dataOffset + tagInfos[i].textOffset;
                    string filename = reader.ReadZeroTerminatedString();
                    if (!tagInfos[i].magic.StartsWith("\0")) filename = filename + "." + tagInfos[i].magic.ToLower();
                    reader.Position = dataInfos[1].dataOffset + tagInfos[i].dataOffset;
                    FileEntry file = new FileEntry()
                    {
                        FileName = filename,
                        FileData = reader.ReadBytes(tagInfos[i + 1].dataOffset - tagInfos[i].dataOffset)
                    };
                    files.Add(file);
                }
                TagInfo lastTag = tagInfos[header.tagCount - 1];
                reader.Position = dataInfos[0].dataOffset + lastTag.textOffset;
                string filename2 = reader.ReadZeroTerminatedString();
                if (!lastTag.magic.StartsWith("\0")) filename2 = filename2 + "." + lastTag.magic.ToLower();
                reader.Position = dataInfos[1].dataOffset + lastTag.dataOffset;
                FileEntry file2 = new FileEntry()
                {
                    FileName = filename2,
                    FileData = reader.ReadBytes(dataInfos[1].dataOffset + dataInfos[1].dataSize - lastTag.dataOffset)
                };
                files.Add(file2);
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
            public int dataOffset;
            public uint unknown4;
            public uint zero1;
            public uint zero2;
            public uint zero3;
            public uint zero4;
        }

        public class TagInfo
        {
            public string magic;
            public int dataOffset;
            public uint flag;
            public int textOffset;
        }


    }
}