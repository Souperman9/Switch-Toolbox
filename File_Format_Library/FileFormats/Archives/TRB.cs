using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.Forms;
using Syroot.BinaryData;
using Toolbox.Library.IO;
using System.Security.Cryptography.X509Certificates;
using SPICA.Formats.Common;
using System.Linq;
using SFGraphics.GLObjects.Shaders.ShaderEventArgs;

namespace FirstPlugin
{
    public class TRB : TreeNodeFile, IArchiveFile, IFileFormat
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; } = true;
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

        public bool CanAddFiles { get; set; } = false;

        public bool CanRenameFiles { get; set; } = true;

        public bool CanReplaceFiles { get; set; } = true;

        public bool CanDeleteFiles { get; set; } = true;

        public List<FileEntry> files = new List<FileEntry>();
        public IEnumerable<ArchiveFileInfo> Files => files;

        public List<Ptex> ptexList = new List<Ptex>();

        public List<DDS> ddsList = new List<DDS>();

        public int ptexCount { get; set; }
        public bool DisplayIcons => throw new NotImplementedException();

        TRB.Header header = new TRB.Header();

        byte[] extraData;

        byte[] relocationData;

        DataInfo[] saveData;

        TagInfo[] saveTag;

        long fileSize;

        public void Load(System.IO.Stream stream)
        {
            fileSize = stream.Length;
            using (var reader = new FileReader(stream))
            {
                ptexCount = 0;
                ptexList.Clear();
                ddsList.Clear();
                // Read header for data locations
                files.Clear();
                reader.ByteOrder = ByteOrder.LittleEndian;
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
                saveData = dataInfos;
                
                for (int i = 0; i < header.tagCount; i++)
                {
                    // Get tags for file extensions, data, and names
                    tagInfos[i] = new TagInfo()
                    {
                        magic = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)),
                        dataOffset = reader.ReadInt32(),
                        flag = reader.ReadUInt32(),
                        textOffset = reader.ReadInt32(),
                        name = "default"
                    };
                }
                

                // Get extra data, currently unused except for saving the file
                if (header.dataInfoCount > 2)
                {
                    reader.Position = dataInfos[2].dataOffset;
                    extraData = reader.ReadBytes(dataInfos[2].dataSize);
                }

                // Get relocation data and write to byte array
                reader.Position = header.relocationDataOffset;
                relocationData = reader.ReadBytes(header.relocationDataSize);

                // Compile filenames and add as files
                for (long i = 0; i < header.tagCount - 1; i++)
                {
                    reader.Position = dataInfos[0].dataOffset + tagInfos[i].textOffset;
                    string filename = reader.ReadZeroTerminatedString();
                    tagInfos[i].name = filename;
                    saveTag = tagInfos;
                    if (!tagInfos[i].magic.StartsWith("\0")) filename = filename + "." + tagInfos[i].magic.ToLower();
                    reader.Position = dataInfos[1].dataOffset + tagInfos[i].dataOffset;
                    FileEntry file = new FileEntry()
                    {
                        FileName = filename,
                        FileData = reader.ReadBytes(tagInfos[i + 1].dataOffset - tagInfos[i].dataOffset)
                    };
                    if (tagInfos[i].magic == "PTEX")
                    {
                        reader.Position = dataInfos[1].dataOffset + tagInfos[i].dataOffset + 88;
                        Ptex ptex = new Ptex()
                        {
                            ptexOffset = reader.Position,
                            width = reader.ReadUInt32(),
                            height = reader.ReadUInt32(),
                            unknown = reader.ReadUInt32(),
                            ddsOffset = reader.ReadUInt32(),
                            ddsSize = reader.ReadInt32()
                        };
                        reader.Position = dataInfos[header.dataInfoCount - 1].dataOffset;
                        reader.Position += ptex.ddsOffset;
                        DDS dds = new DDS(reader.ReadBytes(ptex.ddsSize))
                        {
                            WiiUSwizzle = false,
                            FileType = FileType.Image,
                            Text = filename + ".dds",
                            FileName = filename,
                            CanReplace = true
                        };
                        reader.Position = dataInfos[header.dataInfoCount - 1].dataOffset;
                        reader.Position += ptex.ddsOffset;
                        ptexList.Add(ptex);
                        Nodes.Add(dds);
                        ddsList.Add(dds);
                        FileType = FileType.Image;
                        ptexCount++;
                        //file.FileData = reader.ReadBytes(ptex.ddsSize);
                    }
                    files.Add(file);
                }
                TagInfo lastTag = tagInfos[header.tagCount - 1];
                reader.Position = dataInfos[0].dataOffset + lastTag.textOffset;
                string filename2 = reader.ReadZeroTerminatedString();
                tagInfos[header.tagCount - 1].name = filename2;
                saveTag = tagInfos;
                if (!lastTag.magic.StartsWith("\0")) filename2 = filename2 + "." + lastTag.magic.ToLower();
                reader.Position = dataInfos[1].dataOffset + lastTag.dataOffset;
                FileEntry file2 = new FileEntry()
                {
                    FileName = filename2,
                    FileData = reader.ReadBytes(dataInfos[1].dataSize - lastTag.dataOffset)
                };
                if (tagInfos[header.tagCount - 1].magic == "PTEX")
                {
                    reader.Position = dataInfos[1].dataOffset + tagInfos[header.tagCount - 1].dataOffset + 88;
                    Ptex ptex = new Ptex()
                    {
                        ptexOffset = reader.Position,
                        width = reader.ReadUInt32(),
                        height = reader.ReadUInt32(),
                        unknown = reader.ReadUInt32(),
                        ddsOffset = reader.ReadUInt32(),
                        ddsSize = reader.ReadInt32()
                    };
                    reader.Position = dataInfos[header.dataInfoCount - 1].dataOffset;
                    reader.Position += ptex.ddsOffset;
                    DDS dds = new DDS(reader.ReadBytes(ptex.ddsSize))
                    {
                        WiiUSwizzle = false,
                        FileType = FileType.Image,
                        Text = filename2 + ".dds",
                        FileName = filename2,
                        CanReplace = true,
                    };
                    reader.Position = dataInfos[header.dataInfoCount - 1].dataOffset;
                    reader.Position += ptex.ddsOffset;
                    ptexList.Add(ptex);
                    Nodes.Add(dds);
                    ddsList.Add(dds);
                    FileType = FileType.Image;
                    ptexCount++;
                }
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
            
            using (var writer = new FileWriter(stream))
            {
                writer.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                writer.WriteString("TRB", System.Text.Encoding.ASCII);
                writer.Write(header.version);
                writer.Write(header.flag1);
                writer.Write(header.flag2);
                writer.Write(header.dataInfoCount);
                writer.Write(header.dataInfoSize);
                writer.Write(header.tagCount);
                writer.Write(header.tagSize);
                writer.Write(header.relocationDataOffset);
                writer.Write(header.relocationDataSize);
                writer.Position += 92;


                for (var i = 0; i < header.dataInfoCount; i++)
                {
                    writer.Write(saveData[i].unknown1);
                    writer.Write(saveData[i].textOffset);
                    writer.Write(saveData[i].unknown2);
                    writer.Write(saveData[i].unknown3);
                    writer.Write(saveData[i].dataSize);
                    writer.Write(saveData[i].dataSize2);
                    writer.Write(saveData[i].dataOffset);
                    writer.Write(saveData[i].unknown4);
                    writer.Write(saveData[i].zero1);
                    writer.Write(saveData[i].zero2);
                    writer.Write(saveData[i].zero3);
                    writer.Write(saveData[i].zero4);
                }


                for (var i = 0; i < header.tagCount; i++)
                {
                    writer.WriteNullTerminatedStringUtf8(saveTag[i].magic);
                    writer.Write(saveTag[i].dataOffset);
                    writer.Write(saveTag[i].flag);
                    writer.Write(saveTag[i].textOffset);
                }


                writer.Position = saveData[0].dataOffset;
                writer.WriteNullTerminatedStringUtf8(".text");
                writer.Position += 1;
                writer.WriteNullTerminatedStringUtf8(".data");
                writer.Position += 1;


                for (var i = 0; i < header.tagCount; i++)
                {
                    writer.Position = saveTag[i].textOffset + saveData[0].dataOffset;
                    writer.WriteNullTerminatedStringUtf8(saveTag[i].name);
                }
                writer.Position = saveData[1].dataOffset; 
                
                for (var i = 0; i < header.tagCount; i++)
                {
                    writer.Position = saveData[1].dataOffset + saveTag[i].dataOffset;
                    writer.Write(files[i].FileData);
                }

                if (header.dataInfoCount > 2)
                {
                    writer.Position = saveData[2].dataOffset;
                    writer.Write(extraData);
                }

                writer.Position = header.relocationDataOffset;
                writer.Write(relocationData);
                while (writer.Position < fileSize)
                {
                    writer.Write(new byte());
                }

                if (ptexCount != 0)
                {
                    for (var i = 0; i < ptexCount; i++)
                    {
                        ddsList[i].IsDX10 = false;
                        writer.Position = ptexList[i].ddsOffset + saveData[header.dataInfoCount - 1].dataOffset;
                        writer.WriteString("DDS ", System.Text.Encoding.ASCII);
                        writer.Position -= 1;
                        writer.Write(ddsList[i].header.size);
                        writer.Write(ddsList[i].header.flags);
                        writer.Write(ddsList[i].header.height);
                        writer.Write(ddsList[i].header.width);
                        writer.Write(ddsList[i].header.pitchOrLinearSize);
                        writer.Write(ddsList[i].header.depth);
                        writer.Write(ddsList[i].header.mipmapCount);

                        for (var t = 0; t < ddsList[i].header.reserved1.Length; t++)
                        {
                            writer.Write(ddsList[i].header.reserved1[t]);
                        }

                        writer.Write(ddsList[i].header.ddspf.size);
                        writer.Write(ddsList[i].header.ddspf.flags);
                        writer.Write(ddsList[i].header.ddspf.fourCC);
                        writer.Write(ddsList[i].header.ddspf.RGBBitCount);
                        writer.Write(ddsList[i].header.ddspf.RBitMask);
                        writer.Write(ddsList[i].header.ddspf.GBitMask);
                        writer.Write(ddsList[i].header.ddspf.BBitMask);
                        writer.Write(ddsList[i].header.ddspf.ABitMask);
                        writer.Write(ddsList[i].header.caps);
                        writer.Write(ddsList[i].header.caps2);
                        writer.Write(ddsList[i].header.caps3);
                        writer.Write(ddsList[i].header.caps4);
                        writer.Write(ddsList[i].header.reserved2);
                        writer.Write(ddsList[i].bdata);

                        // Padding
                        while (writer.Position < ptexList[i].ddsSize + saveData[header.dataInfoCount - 1].dataOffset + ptexList[i].ddsOffset)
                        {
                            writer.Write(new byte());
                        }

                    }
                }
            }
        }
        
        public class FileEntry : ArchiveFileInfo
        {

        }

        public class Header
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
            public string name;
        }

        public class Ptex
        {
            public uint width;
            public uint height;
            public uint unknown;
            public uint ddsOffset;
            public int ddsSize;
            public long ptexOffset;
        }

    }
}