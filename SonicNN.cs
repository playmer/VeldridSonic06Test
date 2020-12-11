using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;


namespace SegaNN
{
    namespace Xno
    {
        static class Extensions
        {
            public static Vector3 ReadVector3(this BinaryReader aReader)
            {
                return new Vector3(aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle());
            }

            // This reads an address offset for the SegaNN.Xno
            public static UInt32 ReadAddress(this BinaryReader aReader, InfoHeader aHeader)
            {
                return aReader.ReadUInt32() + aHeader.RootAddress;
            }

            public static string ReadNullTerminatedString(this BinaryReader aReader)
            {
                var nameBytes = new List<char>();

                char character = aReader.ReadChar();
                do
                {
                    nameBytes.Add(character);
                    character = aReader.ReadChar();
                } while (character != '\0');

                return new string(nameBytes.ToArray());
            }
        }

        struct HeaderTag
        {
            public const string Info = "NXIF";
            public const string Texture = "NXTL";
            public const string Effect = "NXEF";
            public const string Object = "NXOB";
            public const string Bones = "NXNN";
            public const string Motion = "NXMO";
            public const string Offset = "NOF0";
            public const string Footer = "NFN0";
            public const string End = "NEND";
        }

        class Header
        {
            UInt32 SectionAddress;
            UInt32 SectionSize;

            public void ReadBase(BinaryReader aReader)
            {
                SectionAddress = (UInt32)aReader.BaseStream.Position - 4;
                SectionSize = aReader.ReadUInt32();
            }

            public void GoToEnd(BinaryReader aReader)
            {
                aReader.BaseStream.Position = SectionAddress + SectionSize + 8;
            }
        }

        class InfoHeader : Header
        {
            public UInt32 SectionCount;
            public UInt32 RootAddress;
            //public UInt32 offset_table_address;
            //public UInt32 offset_table_address_raw;
            //public UInt32 offset_table_size;
            public static InfoHeader Read(BinaryReader aReader)
            {
                var info = new InfoHeader();
                info.ReadBase(aReader);

                info.SectionCount = aReader.ReadUInt32();
                info.RootAddress = aReader.ReadUInt32();

                info.GoToEnd(aReader);
                return info;
            }
        }

        class TextureHeader : Header
        {
            List<Entry> Entries;

            class EntryListHeader
            {
                public UInt32 Count; // Count of entries
                public UInt32 ListAddress; // Address of the list.

                public static EntryListHeader Read(InfoHeader aInfo, BinaryReader aReader)
                {
                    var entryListHeader = new EntryListHeader();

                    entryListHeader.Count = aReader.ReadUInt32();
                    entryListHeader.ListAddress = aReader.ReadUInt32() + aInfo.RootAddress;

                    return entryListHeader;
                }
            }

            class Entry
            {
                string Name; // File name of the texture.
                UInt32 SizeInBytes; // Size of the texture file.
                public static Entry Read(InfoHeader aInfo, BinaryReader aReader)
                {
                    var entry = new Entry();

                    // We read an address offset to a string, followed by the size of the file the string identifies.
                    UInt32 nameAddress = aReader.ReadUInt32() + aInfo.RootAddress;
                    entry.SizeInBytes = aReader.ReadUInt32();

                    // Then we can go to the address itself, and read the string.
                    aReader.BaseStream.Position = nameAddress;
                    entry.Name = aReader.ReadNullTerminatedString();

                    return entry;
                }
            }

            TextureHeader()
            {
                Entries = new List<Entry>();
            }

            public static TextureHeader Read(BinaryReader aReader, InfoHeader aInfo)
            {
                var texture = new TextureHeader();
                texture.ReadBase(aReader);

                // Textures contain an EntryListOffset
                var entryListAddress = aReader.ReadInt32() + aInfo.RootAddress;

                // Addresses we read are offset from the RootAddress stored in the InfoHeader.
                aReader.BaseStream.Position = entryListAddress;

                // Now we read the EntryListHeader
                var entryListHeader = EntryListHeader.Read(aInfo, aReader);

                // Then we read the entries, which are located at ListAddress + (index * 20) + 4
                foreach (int index in Enumerable.Range(0, (int)entryListHeader.Count))
                {
                    aReader.BaseStream.Position = entryListHeader.ListAddress + (index * 20) + 4;

                    var entry = Entry.Read(aInfo, aReader);

                    texture.Entries.Add(entry);
                }

                texture.GoToEnd(aReader);
                return texture;
            }
        }

        class OffsetHeader : Header
        {
            public static OffsetHeader Read(BinaryReader aReader)
            {
                var offset = new OffsetHeader();
                offset.ReadBase(aReader);


                offset.GoToEnd(aReader);
                return offset;
            }
        }

        class EffectHeader : Header
        {
            public static EffectHeader Read(BinaryReader aReader)
            {
                var effect = new EffectHeader();
                effect.ReadBase(aReader);


                effect.GoToEnd(aReader);
                return effect;
            }
        }

        class ObjectHeader : Header
        {
            UInt32 Flag;
            public Vector3 Position; // Center position of the mesh.
            public float Radius;

            List<IndexData> IndexDatas;

            UInt32 TextureCount;

            class MaterialData
            {

            }

            class VertexData
            {

            }

            class IndexData
            {
                UInt32 Flags;
                List<UInt16> Indices;
                List<UInt16> StripSizes;
                List<Vector3> IndicesVec;

                struct IndexEntry
                {
                    public UInt32 Count;
                    public UInt32 Address;

                    public IndexEntry(UInt32 aCount, UInt32 aAddress)
                    {
                        Count = aCount;
                        Address = aAddress;
                    }
                }

                private static IndexData Read(BinaryReader aReader, InfoHeader aInfo, IndexEntry aEntry)
                {
                    var indexData = new IndexData();

                    aReader.BaseStream.Position = aEntry.Address;

                    var indexCount = aReader.ReadUInt32();
                    var morphCount = aReader.ReadUInt32();
                    var morphAddress = aReader.ReadAddress(aInfo);
                    var indexAddress = aReader.ReadAddress(aInfo);


                    // Then comes an array of morphCount StripSizes at morphAddress
                    aReader.BaseStream.Position = morphAddress;

                    foreach (var index in Enumerable.Range(0, (int)morphCount))
                        indexData.StripSizes.Add(aReader.ReadUInt16());

                    foreach (var stripSize in indexData.StripSizes)
                    {

                    }

                    return indexData;
                }

                public static List<IndexData> Read(BinaryReader aReader, InfoHeader aInfo, UInt32 aStartAddress, UInt32 aNumToRead)
                {
                    var indexEntries = new List<IndexEntry>();

                    aReader.BaseStream.Position = aStartAddress;

                    // First comes a list of entries.
                    foreach (int index in Enumerable.Range(0, (int)aNumToRead))
                    {
                        var count = aReader.ReadUInt32();
                        var address = aReader.ReadAddress(aInfo);
                        indexEntries.Add(new IndexEntry(count, address));
                    }

                    var indexDatas = new List<IndexData>();

                    // Then we can read each entry.
                    foreach (var entry in indexEntries)
                        indexDatas.Add(Read(aReader, aInfo, entry));

                    return indexDatas;
                }
            }


            public static ObjectHeader Read(BinaryReader aReader, InfoHeader aInfo)
            {
                var objectSection = new ObjectHeader();
                objectSection.ReadBase(aReader);

                var address = aReader.ReadAddress(aInfo);
                objectSection.Flag = aReader.ReadUInt32();

                aReader.BaseStream.Position = address;

                objectSection.Position = aReader.ReadVector3();
                objectSection.Radius = aReader.ReadSingle();

                UInt32 material_parts_count = aReader.ReadUInt32();
                UInt32 material_parts_address = aReader.ReadAddress(aInfo); ;
                UInt32 vertex_parts_count = aReader.ReadUInt32();
                UInt32 vertex_parts_address = aReader.ReadAddress(aInfo); ;
                UInt32 index_parts_count = aReader.ReadUInt32();
                UInt32 index_parts_address = aReader.ReadAddress(aInfo); ;
                UInt32 bone_parts_count = aReader.ReadUInt32();
                UInt32 bone_set_address = aReader.ReadAddress(aInfo); ;
                UInt32 mesh_count = aReader.ReadUInt32();
                UInt32 mesh_address = aReader.ReadAddress(aInfo);

                objectSection.TextureCount = aReader.ReadUInt32();

                objectSection.IndexDatas = IndexData.Read(aReader, aInfo, index_parts_address, index_parts_count);

                objectSection.GoToEnd(aReader);
                return objectSection;
            }
        }

        class BoneHeader : Header
        {
            public static BoneHeader Read(BinaryReader aReader)
            {
                var bones = new BoneHeader();
                bones.ReadBase(aReader);


                bones.GoToEnd(aReader);
                return bones;
            }
        }

        class MotionHeader : Header
        {
            public static MotionHeader Read(BinaryReader aReader)
            {
                var motion = new MotionHeader();
                motion.ReadBase(aReader);


                motion.GoToEnd(aReader);
                return motion;
            }
        }

        class FooterHeader : Header
        {
            public static FooterHeader Read(BinaryReader aReader)
            {
                var footer = new FooterHeader();
                footer.ReadBase(aReader);


                footer.GoToEnd(aReader);
                return footer;
            }
        }

        class EndHeader : Header
        {
            public static EndHeader Read(BinaryReader aReader)
            {
                var end = new EndHeader();
                end.ReadBase(aReader);


                end.GoToEnd(aReader);
                return end;
            }
        }

        // Represents all the data in an individual Xno file, allows Reading and Writing Xno files.
        class File
        {
            public File() { }

            public File(string aFile)
            {
                using (var reader = new BinaryReader(System.IO.File.Open(aFile, FileMode.Open)))
                {
                    ReadFile(reader);
                }
            }

            public File(BinaryReader aReader)
            {
                ReadFile(aReader);
            }

            private void ReadFile(BinaryReader aReader)
            {
                string identifier = new string(aReader.ReadChars(4));

                // The first section should be the Info Section Header.
                Trace.Assert(HeaderTag.Info.Equals(identifier));

                var info = Xno.InfoHeader.Read(aReader);

                // Now we read the rest of the sections
                for (var i = 0; i < info.SectionCount; ++i)
                {
                    identifier = new string(aReader.ReadChars(4));

                    switch (identifier)
                    {
                        case HeaderTag.Texture: TextureHeader.Read(aReader, info); break;
                        case HeaderTag.Effect: EffectHeader.Read(aReader); break;
                        case HeaderTag.Bones: BoneHeader.Read(aReader); break;
                        case HeaderTag.Object: ObjectHeader.Read(aReader, info); break;
                        case HeaderTag.Motion: MotionHeader.Read(aReader); break;
                    }
                }

                identifier = new string(aReader.ReadChars(4));
                Trace.Assert(HeaderTag.Offset.Equals(identifier));
                var offsetHeader = OffsetHeader.Read(aReader);

                identifier = new string(aReader.ReadChars(4));
                Trace.Assert(HeaderTag.Footer.Equals(identifier));
                var footerHeader = FooterHeader.Read(aReader);

                identifier = new string(aReader.ReadChars(4));
                Trace.Assert(HeaderTag.End.Equals(identifier));
                var endHeader = EndHeader.Read(aReader);
            }

        }
    }
}

namespace VeldridTest
{


    class Sonic06XnoReader
    {
        public struct Vertex
        {
            public const UInt32 SizeInBytes = 20;

            public Vector3 Translation;
            public Vector2 TextureCoordinates;

            public Vertex(Vector3 aTranslation, Vector2 aTextureCoordinates)
            {
                Translation = aTranslation;
                TextureCoordinates = aTextureCoordinates;
            }
        }

        public Sonic06XnoReader(string aFile)
        {
            using (var reader = new BinaryReader(File.Open(aFile, FileMode.Open)))
            {
                ReadFile(reader);
            }
        }

        private void ReadFile(BinaryReader aReader)
        {
            var file = new SegaNN.Xno.File(aReader);
        }
    }
}
