using System;
using System.Collections.Generic;
using System.IO;
using Toolbox.Library;
using Syroot.BinaryData;
using Toolbox.Library.IO;
using SPICA.Formats.Common;
using Toolbox.Library.Rendering;

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

        public List<Pmdl> pmdlList = new List<Pmdl>();

        public int ptexCount { get; set; }

        public int pmdlCount { get; set; }
        public bool DisplayIcons => throw new NotImplementedException();

        TRB.Header header = new TRB.Header();

        byte[] extraData;

        byte[] relocationData;

        DataInfo[] saveData;

        TagInfo[] saveTag;

        long fileSize;

        ByteOrder byteOrder = ByteOrder.LittleEndian;


        public void Load(System.IO.Stream stream)
        {
            fileSize = stream.Length;
            using (var reader = new FileReader(stream))
            {
                pmdlCount = 0;
                ptexCount = 0;
                ptexList.Clear();
                ddsList.Clear();
                // Read header for data locations
                files.Clear();
                reader.Position = 4;
                reader.ByteOrder = byteOrder;
                if (reader.ReadUInt32() != 2001) byteOrder = ByteOrder.BigEndian;
                reader.ByteOrder = byteOrder;
                reader.Position = 0;
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
                    reader.Position = dataInfos[1].dataOffset + tagInfos[i].dataOffset;

                    // Load textures as a dds using the PTEX pointer
                    if (tagInfos[i].magic == "PTEX")
                    {
                        reader.Position += 88;
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
                    }

                    if (tagInfos[i].magic == "PMDL")
                    {
                        reader.Position += 8;
                        Pmdl pmdl = new Pmdl()
                        {
                            relocationDataCount = reader.ReadInt16(),
                            pmdlSize = reader.ReadInt16()
                        };
                        reader.Position += 4;
                        pmdl.modelTextOffset = reader.ReadInt32();
                        reader.Position += 36;
                        pmdl.sumVertCount = reader.ReadInt32();
                        pmdl.faceStartOffsetRelative = reader.ReadInt32();
                        pmdl.vertexStartOffset = reader.ReadInt32();
                        reader.Position += 4;
                        pmdl.sumFaceCount = reader.ReadInt32();
                        pmdl.faceStartOffset = reader.ReadInt32();
                        reader.Position += 48;
                        pmdl.subInfoCount = reader.ReadInt32();
                        pmdl.offsetToSubInfosStart = reader.ReadInt32();
                        pmdl.endOfSubInfos = reader.ReadInt32();
                        reader.Position = pmdl.offsetToSubInfosStart + dataInfos[1].dataOffset;
                        int[] subInfoStarts = new int[pmdl.subInfoCount];
                        subInfoStarts = reader.ReadInt32s(pmdl.subInfoCount);
                        SubInfoData[] subInfoDatas = new SubInfoData[pmdl.subInfoCount];
                        for (int t = 0; t < pmdl.subInfoCount; t++)
                        {
                            reader.Position = subInfoStarts[t] + dataInfos[1].dataOffset;
                            SubInfoData subInfoData = new SubInfoData()
                            {
                                unknown1 = reader.ReadInt32(),
                                unknown2 = reader.ReadInt32(),
                                unknown3 = reader.ReadInt32(),
                                unknown4 = reader.ReadInt32(),
                                unknown5 = reader.ReadInt32(),
                                unknown6 = reader.ReadInt32(),
                                vertexCount = reader.ReadInt32(),
                                unknown8 = reader.ReadInt32(),
                                previousFaceCount = reader.ReadInt32(),
                                faceCount = reader.ReadInt32(),
                                unknown11 = reader.ReadInt32(),
                                unknown12 = reader.ReadInt32(),
                                vertexOffsetRelative = reader.ReadInt32(),
                                normalUVOffset = reader.ReadInt32(),
                                faceOffset = reader.ReadInt32(),
                                sameSizeorOffset = reader.ReadInt32(),
                                sameSizeorOffset2 = reader.ReadInt32(),
                                sameSizeorOffset3 = reader.ReadInt32(),
                                sameSizeorOffset4 = reader.ReadInt32(),
                                sameSizeorOffset5 = reader.ReadInt32()
                            };

                            pmdl.verticesCount = subInfoData.vertexCount;
                            pmdl.facesCount = subInfoData.faceCount;
                            subInfoDatas[t] = subInfoData;
                        }
                        var renderedMesh = new GenericRenderedObject();
                        var renderer = new GenericModelRenderer();
                        renderedMesh.ImageKey = "mesh";
                        renderedMesh.SelectedImageKey = "mesh";
                        renderedMesh.Checked = true;
                        int[] normalUVSize = new int[pmdl.subInfoCount];
                        if (pmdl.subInfoCount > 1)
                        {
                            int remember = 0;
                            for (int a = 0; a + 1 < pmdl.subInfoCount; a++)
                            {
                                pmdl.normalUVStart = subInfoDatas[a].normalUVOffset;
                                pmdl.normalUVEnd = subInfoDatas[a + 1].normalUVOffset;
                                normalUVSize[a] = (int)(pmdl.normalUVEnd - pmdl.normalUVStart);
                                remember = a;
                            }
                            pmdl.normalUVStart = subInfoDatas[remember + 1].normalUVOffset;
                            pmdl.normalUVEnd = subInfoDatas[remember + 1].normalUVOffset;
                        }
                        else if (pmdl.subInfoCount >= 1)
                        {
                            for (int x = 0; x < pmdl.subInfoCount; x++)
                            {
                                int stride = (int)(normalUVSize[x] / subInfoDatas[x].vertexCount);
                                reader.Position = pmdl.vertexStartOffset + subInfoDatas[x].vertexOffsetRelative;
                                for (int j = 0; j < subInfoDatas[x].vertexCount; j++)
                                {
                                    Vertex vert = new Vertex();
                                    vert.pos = new OpenTK.Vector3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                                }

                            }
                        }
                    }
                    files.Add(file);
                }

                // For the last file, read until the end of the raw data section
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
                if (tagInfos[0].magic == "enti")
                {
                    reader.Position = dataInfos[1].dataOffset + tagInfos[0].dataOffset;
                    var position = reader.Position;
                    var entityHeader = new Entity.EntityHeader()
                    {
                        entitiesOffset = reader.ReadUInt32(),
                        entitiesCount = reader.ReadUInt32(),
                        unknown1 = reader.ReadUInt32(),
                        unknown2 = reader.ReadUInt32()
                    };
                    reader.Position = position + entityHeader.entitiesOffset;
                    var properties = new Entity.Property[entityHeader.entitiesCount];
                    //var entities = new Entity.Entity[entityHeader.entitiesCount];
                    //for (int i = 0; i < entityHeader.entitiesCount; i++)
                    //{
                    //    entities[i].entityNameOffset = reader.ReadUInt32();
                    //    entities[i].propertiesCount = reader.ReadUInt16();
                    //    entities[i].propertiesCount2 = reader.ReadUInt16();
                    //    entities[i].propertiesOffset = reader.ReadUInt32();
                    //    entities[i].matrixOffset = reader.ReadUInt32();
                    //    entities[i].positionOffset = reader.ReadUInt32();
                    //    entities[i].unknown = reader.ReadUInt32();
                    //    entities[i].flag = reader.ReadUInt32();
                    //    entities[i].valuesOffset = reader.ReadUInt32();
                    //}

                    var entities = new Entity.Entity[entityHeader.entitiesCount];
 

                    for (int i = 0; i < entityHeader.entitiesCount; i++)
                    {
                        reader.Position = position + entities[i].propertiesOffset;
                    }



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
                writer.ByteOrder = byteOrder;
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

                // Save textures
                if (ptexCount != 0)
                {
                    for (var i = 0; i < ptexCount; i++)
                    {
                        // I couldn't figure out how to directly write a complete dds with one line - either I'm dumb or the dds code is dumb
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

        public class Pmdl
        {
            public float vertices;
            public float normals;
            public float uvs;
            public long verticesCount;
            public long facesCount;
            public long normalUVStart;
            public long normalUVEnd;
            public ulong faces;
            public short relocationDataCount;
            public short pmdlSize;
            public long modelTextOffset;
            public long sumVertCount;
            public long faceStartOffsetRelative;
            public long vertexStartOffset;
            public long sumFaceCount;
            public long faceStartOffset;
            public int subInfoCount;
            public long offsetToSubInfosStart;
            public long endOfSubInfos;
        }

        public class SubInfoData
        {
            public long unknown1;
            public long unknown2;
            public long unknown3;
            public long unknown4;
            public long unknown5;
            public long unknown6;
            public long vertexCount;
            public long unknown8;
            public long previousFaceCount;
            public long faceCount;
            public long unknown11;
            public long unknown12;
            public long vertexOffsetRelative;
            public long normalUVOffset;
            public long faceOffset;
            public long sameSizeorOffset;
            public long sameSizeorOffset2;
            public long sameSizeorOffset3;
            public long sameSizeorOffset4;
            public long sameSizeorOffset5;
        }
    }

    namespace Entity
    {
        public enum VariableType
        {
            ENUM = 0,
            INT = 1,
            FLOAT = 2,
            BOOL = 3,
            TEXTOFFSET = 4,
            VECTOR4 = 5,
            Unknown = 6,
            Unknown2 = 7,
            Unknown3 = 8,
            OFFSET = 9
        };

        public class EntityHeader
        {
            public uint entitiesOffset;
            public uint entitiesCount;
            public uint unknown1;
            public uint unknown2;
        }

        public class Entity
        {
            public uint entityNameOffset;
            public ushort propertiesCount;
            public ushort propertiesCount2;
            public uint propertiesOffset;
            public uint matrixOffset;
            public uint positionOffset;
            public uint unknown;
            public uint flag;
            public uint valuesOffset;
            public Property[] properties;

            public Entity(
                uint entityNameOffset,
                ushort propertiesCount,
                ushort propertiesCount2,
                uint propertiesOffset,
                uint matrixOffset,
                uint positionOffset,
                uint unknown,
                uint flag,
                uint valuesOffset,
                Property[] properties
            )
            {
                this.entityNameOffset = entityNameOffset;
                this.propertiesCount = propertiesCount;
                this.propertiesCount2 = propertiesCount2;
                this.propertiesOffset = propertiesOffset;
                this.matrixOffset = matrixOffset;
                this.positionOffset = positionOffset;
                this.unknown = unknown;
                this.flag = flag;
                this.valuesOffset = valuesOffset;
                this.properties = properties;
            }
        }
        public class Property
        {
            public uint propertyNameOffset;
            VariableType type;
            public object value;
        }
    }
}