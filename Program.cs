using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Globalization;
using Nyerguds.Ini;
using System.Drawing.Imaging;
using System.IO;

namespace TS_RA2_YR_Thumbnails_Extractor
{
    class Program
    {
        static void Main(string[] args)
        {
            // Make sure the Parse() functions parse commas and periods correctly
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            // DON'T FORGET TO CALL THIS FUNCTION
            MapThumbnailExtractor.Load();

            var MapThumb = new MapThumbnailExtractor("test.map", 1);
            MapThumb.Get_Bitmap().Save("test.png");
        }
    }

    class MapThumbnailExtractor
    {
        WaypointStruct[] Waypoints = new WaypointStruct[8];
        IniFile MapINI;
        Bitmap Preview;
        Rectangle LocalSize;
        static Bitmap[] SpawnLocationBitmaps = new Bitmap[8];

        static public void Load()
        {
            SpawnLocationBitmaps[0] = TS_RA2_YR_Thumbnails_Extractor.Resource1._1;
            SpawnLocationBitmaps[1] = TS_RA2_YR_Thumbnails_Extractor.Resource1._2;
            SpawnLocationBitmaps[2] = TS_RA2_YR_Thumbnails_Extractor.Resource1._3;
            SpawnLocationBitmaps[3] = TS_RA2_YR_Thumbnails_Extractor.Resource1._4;
            SpawnLocationBitmaps[4] = TS_RA2_YR_Thumbnails_Extractor.Resource1._5;
            SpawnLocationBitmaps[5] = TS_RA2_YR_Thumbnails_Extractor.Resource1._6;
            SpawnLocationBitmaps[6] = TS_RA2_YR_Thumbnails_Extractor.Resource1._7;
            SpawnLocationBitmaps[7] = TS_RA2_YR_Thumbnails_Extractor.Resource1._8;
        }

        public unsafe MapThumbnailExtractor(string FileName, int ScaleFactor)
        {
            MapINI = new IniFile(FileName);

            String[] size = MapINI.getStringValue("Preview", "Size", "").Split(',');
            var previewSize = new Rectangle(int.Parse(size[0]), int.Parse(size[1]), int.Parse(size[2]), int.Parse(size[3]));
            Preview = new Bitmap(previewSize.Width, previewSize.Height, PixelFormat.Format24bppRgb);

            byte[] image = new byte[Preview.Width * Preview.Height * 3];

            string[] LocalSizeString = MapINI.getStringValue("Map", "LocalSize", "").Split(',');
            LocalSize = new Rectangle(int.Parse(LocalSizeString[0]), int.Parse(LocalSizeString[1]), int.Parse(LocalSizeString[2]), int.Parse(LocalSizeString[3]));


            var SectionKeyValues = MapINI.getSectionContent("PreviewPack");

            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, string> entry in SectionKeyValues)
            {
                sb.Append(entry.Value);
            }
            String Base64String = sb.ToString();
            var image_compressed = Convert.FromBase64String(Base64String);

            Format5.DecodeInto(image_compressed, image, 5);

            // invert rgb->bgr
            BitmapData bmd = Preview.LockBits(new Rectangle(0, 0, Preview.Width, Preview.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            int idx = 0;
            for (int y = 0; y < bmd.Height; y++)
            {
                byte* row = (byte*)bmd.Scan0 + bmd.Stride * y;
                byte* p = row;
                for (int x = 0; x < bmd.Width; x++)
                {
                    byte b = image[idx++];
                    byte g = image[idx++];
                    byte r = image[idx++];
                    *p++ = r;
                    *p++ = g;
                    *p++ = b;
                }
            }
            // spawn locations

            var WaypointsSectionKeyValues = MapINI.getSectionContent("Waypoints");

            if (WaypointsSectionKeyValues != null)
            {
                foreach (KeyValuePair<string, string> entry in WaypointsSectionKeyValues)
                {
                    int WayPoint = int.Parse(entry.Key);

                    if (WayPoint > 7) continue;

                    int Pos = int.Parse(entry.Value);

                    int WayRY = Pos / 1000;
                    int WayRX = Pos - WayRY * 1000;

                    int WayDX = WayRX - WayRY + LocalSize.Width - 1;
                    int WayDY = WayRX + WayRY - LocalSize.Width - 1;


                    const int ManualAdjustX = 6; // Tested with scalefactor 1
                    const int ManualAdjustY = -3; // Tested with scalefactor 1

                    Waypoints[WayPoint].WasFound = true;
                    Waypoints[WayPoint].Y = (WayDY / 2) + ManualAdjustY;
                    Waypoints[WayPoint].X = WayDX + ManualAdjustX;

                }

            }
            Preview.UnlockBits(bmd);

            Graphics g_ = Graphics.FromImage(Preview);

            Draw_Spawn_Locations(ref g_, ScaleFactor);
            g_.Flush();
        }

        public Bitmap Get_Bitmap()
        {
            return Preview;
        }

        void Draw_Spawn_Locations(ref Graphics g, int _ScaleFactor)
        {
            for (int i = 0; i < 8; i++)
            {
                Draw_Spawn_Location(ref g, i, _ScaleFactor);
            }
        }


        void Draw_Spawn_Location(ref Graphics g, int SpawnNumber, int _ScaleFactor)
        {
            WaypointStruct Waypoint = Waypoints[SpawnNumber];
            if (Waypoint.WasFound == false) return;
            if (SpawnLocationBitmaps[SpawnNumber] == null) return;

            // Console.WriteLine("draw spawn: X = {0}, Y = {1}", Waypoint.X, Waypoint.Y);

            var Spawn = SpawnLocationBitmaps[SpawnNumber];
            int SpawnX = Spawn.Height / (2 * _ScaleFactor);
            int SpawnY = Spawn.Width / (2 * _ScaleFactor);
            g.DrawImage(Spawn, (Waypoint.X - SpawnX)  * _ScaleFactor, (Waypoint.Y - SpawnY) * _ScaleFactor, Spawn.Width, Spawn.Height);

            g.Flush();
        }
    }


    struct WaypointStruct
    {
        public bool WasFound;
        public int X;
        public int Y;
    }

    public class Format5
    {
        public static unsafe uint DecodeInto(byte[] src, byte[] dest, int format = 5)
        {
            fixed (byte* pr = src, pw = dest)
            {
                byte* r = pr, w = pw;
                byte* w_end = w + dest.Length;

                while (w < w_end)
                {
                    ushort size_in = *(ushort*)r;
                    r += 2;
                    uint size_out = *(ushort*)r;
                    r += 2;

                    if (size_in == 0 || size_out == 0)
                        break;

                    if (format == 80)
                        Format80.DecodeInto(r, w);
                    else
                        MiniLZO.Decompress(r, size_in, w, ref size_out);
                    r += size_in;
                    w += size_out;
                }
                return (uint)(w - pw);
            }
        }
    }
    public static class MiniLZO
    {

        unsafe static uint lzo1x_1_compress_core(byte* @in, uint in_len, byte* @out, ref uint out_len, uint ti, void* wrkmem)
        {
            byte* ip;
            byte* op;
            byte* in_end = @in + in_len;
            byte* ip_end = @in + in_len - 20;
            byte* ii;
            ushort* dict = (ushort*)wrkmem;
            op = @out;
            ip = @in;
            ii = ip;
            ip += ti < 4 ? 4 - ti : 0;

            byte* m_pos;
            uint m_off;
            uint m_len;

            for (; ; )
            {

                uint dv;
                uint dindex;
            literal:
                ip += 1 + ((ip - ii) >> 5);
            next:
                if (ip >= ip_end)
                    break;
                dv = (*(uint*)(void*)(ip));
                dindex = ((uint)(((((((uint)((0x1824429d) * (dv)))) >> (32 - 14))) & (((1u << (14)) - 1) >> (0))) << (0)));
                m_pos = @in + dict[dindex];
                dict[dindex] = ((ushort)((uint)((ip) - (@in))));
                if (dv != (*(uint*)(void*)(m_pos)))
                    goto literal;

                ii -= ti; ti = 0;
                {
                    uint t = ((uint)((ip) - (ii)));
                    if (t != 0)
                    {
                        if (t <= 3)
                        {
                            op[-2] |= ((byte)(t));
                            *(uint*)(op) = *(uint*)(ii);
                            op += t;
                        }
                        else if (t <= 16)
                        {
                            *op++ = ((byte)(t - 3));
                            *(uint*)(op) = *(uint*)(ii);
                            *(uint*)(op + 4) = *(uint*)(ii + 4);
                            *(uint*)(op + 8) = *(uint*)(ii + 8);
                            *(uint*)(op + 12) = *(uint*)(ii + 12);
                            op += t;
                        }
                        else
                        {
                            if (t <= 18)
                                *op++ = ((byte)(t - 3));
                            else
                            {
                                uint tt = t - 18;
                                *op++ = 0;
                                while (tt > 255)
                                {
                                    tt -= 255;
                                    *(byte*)op++ = 0;
                                }

                                *op++ = ((byte)(tt));
                            }
                            do
                            {
                                *(uint*)(op) = *(uint*)(ii);
                                *(uint*)(op + 4) = *(uint*)(ii + 4);
                                *(uint*)(op + 8) = *(uint*)(ii + 8);
                                *(uint*)(op + 12) = *(uint*)(ii + 12);
                                op += 16; ii += 16; t -= 16;
                            } while (t >= 16); if (t > 0) { do *op++ = *ii++; while (--t > 0); }
                        }
                    }
                }
                m_len = 4;
                {
                    uint v;
                    v = (*(uint*)(void*)(ip + m_len)) ^ (*(uint*)(void*)(m_pos + m_len));
                    if (v == 0)
                    {
                        do
                        {
                            m_len += 4;
                            v = (*(uint*)(void*)(ip + m_len)) ^ (*(uint*)(void*)(m_pos + m_len));
                            if (ip + m_len >= ip_end)
                                goto m_len_done;
                        } while (v == 0);
                    }
                    m_len += (uint)lzo_bitops_ctz32(v) / 8;
                }
            m_len_done:
                m_off = ((uint)((ip) - (m_pos)));
                ip += m_len;
                ii = ip;
                if (m_len <= 8 && m_off <= 0x0800)
                {
                    m_off -= 1;
                    *op++ = ((byte)(((m_len - 1) << 5) | ((m_off & 7) << 2)));
                    *op++ = ((byte)(m_off >> 3));
                }
                else if (m_off <= 0x4000)
                {
                    m_off -= 1;
                    if (m_len <= 33)
                        *op++ = ((byte)(32 | (m_len - 2)));
                    else
                    {
                        m_len -= 33;
                        *op++ = 32 | 0;
                        while (m_len > 255)
                        {
                            m_len -= 255;
                            *(byte*)op++ = 0;
                        }
                        *op++ = ((byte)(m_len));
                    }
                    *op++ = ((byte)(m_off << 2));
                    *op++ = ((byte)(m_off >> 6));
                }
                else
                {
                    m_off -= 0x4000;
                    if (m_len <= 9)
                        *op++ = ((byte)(16 | ((m_off >> 11) & 8) | (m_len - 2)));
                    else
                    {
                        m_len -= 9;
                        *op++ = ((byte)(16 | ((m_off >> 11) & 8)));
                        while (m_len > 255)
                        {
                            m_len -= 255;
                            *(byte*)op++ = 0;
                        }
                        *op++ = ((byte)(m_len));
                    }
                    *op++ = ((byte)(m_off << 2));
                    *op++ = ((byte)(m_off >> 6));
                }
                goto next;
            }
            out_len = ((uint)((op) - (@out)));
            return ((uint)((in_end) - (ii - ti)));
        }

        static int[] MultiplyDeBruijnBitPosition = {
			  0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8, 
			  31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
			};
        private static int lzo_bitops_ctz32(uint v)
        {
            return MultiplyDeBruijnBitPosition[((uint)((v & -v) * 0x077CB531U)) >> 27];
        }

        unsafe static int lzo1x_1_compress(byte* @in, uint in_len, byte* @out, ref uint out_len, byte* wrkmem)
        {
            byte* ip = @in;
            byte* op = @out;
            uint l = in_len;
            uint t = 0;
            while (l > 20)
            {
                uint ll = l;
                ulong ll_end;
                ll = ((ll) <= (49152) ? (ll) : (49152));
                ll_end = (ulong)ip + ll;
                if ((ll_end + ((t + ll) >> 5)) <= ll_end || (byte*)(ll_end + ((t + ll) >> 5)) <= ip + ll)
                    break;

                for (int i = 0; i < (1 << 14) * sizeof(ushort); i++)
                    wrkmem[i] = 0;
                t = lzo1x_1_compress_core(ip, ll, op, ref out_len, t, wrkmem);
                ip += ll;
                op += out_len;
                l -= ll;
            }
            t += l;
            if (t > 0)
            {
                byte* ii = @in + in_len - t;
                if (op == @out && t <= 238)
                    *op++ = ((byte)(17 + t));
                else if (t <= 3)
                    op[-2] |= ((byte)(t));
                else if (t <= 18)
                    *op++ = ((byte)(t - 3));
                else
                {
                    uint tt = t - 18;
                    *op++ = 0;
                    while (tt > 255)
                    {
                        tt -= 255;
                        *(byte*)op++ = 0;
                    }

                    *op++ = ((byte)(tt));
                }
                do *op++ = *ii++; while (--t > 0);
            }
            *op++ = 16 | 1;
            *op++ = 0;
            *op++ = 0;
            out_len = ((uint)((op) - (@out)));
            return 0;
        }

        public unsafe static int lzo1x_decompress(byte* @in, uint in_len, byte* @out, ref uint out_len, void* wrkmem)
        {
            byte* op;
            byte* ip;
            uint t;
            byte* m_pos;
            byte* ip_end = @in + in_len;
            out_len = 0;
            op = @out;
            ip = @in;
            bool gt_first_literal_run = false;
            bool gt_match_done = false;
            if (*ip > 17)
            {
                t = (uint)(*ip++ - 17);
                if (t < 4)
                {
                    match_next(ref op, ref ip, ref t);
                }
                else
                {
                    do *op++ = *ip++; while (--t > 0);
                    gt_first_literal_run = true;
                }
            }
            while (true)
            {
                if (gt_first_literal_run)
                {
                    gt_first_literal_run = false;
                    goto first_literal_run;
                }

                t = *ip++;
                if (t >= 16)
                    goto match;
                if (t == 0)
                {
                    while (*ip == 0)
                    {
                        t += 255;
                        ip++;
                    }
                    t += (uint)(15 + *ip++);
                }
                *(uint*)op = *(uint*)ip;
                op += 4; ip += 4;
                if (--t > 0)
                {
                    if (t >= 4)
                    {
                        do
                        {
                            *(uint*)op = *(uint*)ip;
                            op += 4; ip += 4; t -= 4;
                        } while (t >= 4);
                        if (t > 0) do *op++ = *ip++; while (--t > 0);
                    }
                    else
                        do *op++ = *ip++; while (--t > 0);
                }
            first_literal_run:
                t = *ip++;
                if (t >= 16)
                    goto match;
                m_pos = op - (1 + 0x0800);
                m_pos -= t >> 2;
                m_pos -= *ip++ << 2;

                *op++ = *m_pos++; *op++ = *m_pos++; *op++ = *m_pos;
                gt_match_done = true;

            match:
                do
                {
                    if (gt_match_done)
                    {
                        gt_match_done = false;
                        goto match_done;
                        ;
                    }
                    if (t >= 64)
                    {
                        m_pos = op - 1;
                        m_pos -= (t >> 2) & 7;
                        m_pos -= *ip++ << 3;
                        t = (t >> 5) - 1;

                        copy_match(ref op, ref m_pos, ref t);
                        goto match_done;
                    }
                    else if (t >= 32)
                    {
                        t &= 31;
                        if (t == 0)
                        {
                            while (*ip == 0)
                            {
                                t += 255;
                                ip++;
                            }
                            t += (uint)(31 + *ip++);
                        }
                        m_pos = op - 1;
                        m_pos -= (*(ushort*)(void*)(ip)) >> 2;
                        ip += 2;
                    }
                    else if (t >= 16)
                    {
                        m_pos = op;
                        m_pos -= (t & 8) << 11;
                        t &= 7;
                        if (t == 0)
                        {
                            while (*ip == 0)
                            {
                                t += 255;
                                ip++;
                            }
                            t += (uint)(7 + *ip++);
                        }
                        m_pos -= (*(ushort*)ip) >> 2;
                        ip += 2;
                        if (m_pos == op)
                            goto eof_found;
                        m_pos -= 0x4000;
                    }
                    else
                    {
                        m_pos = op - 1;
                        m_pos -= t >> 2;
                        m_pos -= *ip++ << 2;
                        *op++ = *m_pos++; *op++ = *m_pos;
                        goto match_done;
                    }

                    if (t >= 2 * 4 - (3 - 1) && (op - m_pos) >= 4)
                    {
                        *(uint*)op = *(uint*)m_pos;
                        op += 4; m_pos += 4; t -= 4 - (3 - 1);
                        do
                        {
                            *(uint*)op = *(uint*)m_pos;
                            op += 4; m_pos += 4; t -= 4;
                        } while (t >= 4);
                        if (t > 0) do *op++ = *m_pos++; while (--t > 0);
                    }
                    else
                    {
                        // copy_match:
                        *op++ = *m_pos++; *op++ = *m_pos++;
                        do *op++ = *m_pos++; while (--t > 0);
                    }
                match_done:
                    t = (uint)(ip[-2] & 3);
                    if (t == 0)
                        break;
                    // match_next:
                    *op++ = *ip++;
                    if (t > 1) { *op++ = *ip++; if (t > 2) { *op++ = *ip++; } }
                    t = *ip++;
                } while (true);
            }
        eof_found:

            out_len = ((uint)((op) - (@out)));
            return (ip == ip_end ? 0 :
                   (ip < ip_end ? (-8) : (-4)));
        }

        private static unsafe void match_next(ref byte* op, ref byte* ip, ref uint t)
        {
            do *op++ = *ip++; while (--t > 0);
            t = *ip++;
        }

        private static unsafe void copy_match(ref byte* op, ref byte* m_pos, ref uint t)
        {
            *op++ = *m_pos++; *op++ = *m_pos++;
            do *op++ = *m_pos++; while (--t > 0);
        }



        public static unsafe byte[] Decompress(byte[] @in, byte[] @out)
        {
            uint out_len = 0;
            fixed (byte* @pIn = @in, wrkmem = new byte[IntPtr.Size * 16384], pOut = @out)
            {
                lzo1x_decompress(pIn, (uint)@in.Length, @pOut, ref @out_len, wrkmem);
            }
            return @out;
        }

        public static unsafe void Decompress(byte* r, uint size_in, byte* w, ref uint size_out)
        {
            fixed (byte* wrkmem = new byte[IntPtr.Size * 16384])
            {
                lzo1x_decompress(r, size_in, w, ref size_out, wrkmem);
            }
        }

        public static unsafe byte[] Compress(byte[] input)
        {
            byte[] @out = new byte[input.Length + (input.Length / 16) + 64 + 3];
            uint out_len = 0;
            fixed (byte* @pIn = input, wrkmem = new byte[IntPtr.Size * 16384], pOut = @out)
            {
                lzo1x_1_compress(pIn, (uint)input.Length, @pOut, ref @out_len, wrkmem);
            }
            Array.Resize(ref @out, (int)out_len);
            return @out;
        }

        public static unsafe void Compress(byte* r, uint size_in, byte* w, ref uint size_out)
        {
            fixed (byte* wrkmem = new byte[IntPtr.Size * 16384])
            {
                lzo1x_1_compress(r, size_in, w, ref size_out, wrkmem);
            }
        }
    }

    class FastByteReader
    {
        readonly byte[] src;
        int offset = 0;

        public FastByteReader(byte[] src)
        {
            this.src = src;
        }

        public bool Done() { return offset >= src.Length; }
        public byte ReadByte() { return src[offset++]; }
        public int ReadWord()
        {
            int x = ReadByte();
            return x | (ReadByte() << 8);
        }

        public void CopyTo(byte[] dest, int offset, int count)
        {
            Array.Copy(src, this.offset, dest, offset, count);
            this.offset += count;
        }

        public int Remaining() { return src.Length - offset; }
    }

    public static class Format80
    {
        static void ReplicatePrevious(byte[] dest, int destIndex, int srcIndex, int count)
        {
            if (srcIndex > destIndex)
                throw new NotImplementedException(string.Format("srcIndex > destIndex {0} {1}", srcIndex, destIndex));

            if (destIndex - srcIndex == 1)
            {
                for (int i = 0; i < count; i++)
                    dest[destIndex + i] = dest[destIndex - 1];
            }
            else
            {
                for (int i = 0; i < count; i++)
                    dest[destIndex + i] = dest[srcIndex + i];
            }
        }

        public static int DecodeInto(byte[] src, byte[] dest)
        {
            var ctx = new FastByteReader(src);
            int destIndex = 0;

            while (true)
            {
                byte i = ctx.ReadByte();
                if ((i & 0x80) == 0)
                {
                    // case 2
                    byte secondByte = ctx.ReadByte();
                    int count = ((i & 0x70) >> 4) + 3;
                    int rpos = ((i & 0xf) << 8) + secondByte;

                    ReplicatePrevious(dest, destIndex, destIndex - rpos, count);
                    destIndex += count;
                }
                else if ((i & 0x40) == 0)
                {
                    // case 1
                    int count = i & 0x3F;
                    if (count == 0)
                        return destIndex;

                    ctx.CopyTo(dest, destIndex, count);
                    destIndex += count;
                }
                else
                {
                    int count3 = i & 0x3F;
                    if (count3 == 0x3E)
                    {
                        // case 4
                        int count = ctx.ReadWord();
                        byte color = ctx.ReadByte();

                        for (int end = destIndex + count; destIndex < end; destIndex++)
                            dest[destIndex] = color;
                    }
                    else if (count3 == 0x3F)
                    {
                        // case 5
                        int count = ctx.ReadWord();
                        int srcIndex = ctx.ReadWord();
                        if (srcIndex >= destIndex)
                            throw new NotImplementedException(string.Format("srcIndex >= destIndex {0} {1}", srcIndex, destIndex));

                        for (int end = destIndex + count; destIndex < end; destIndex++)
                            dest[destIndex] = dest[srcIndex++];
                    }
                    else
                    {
                        // case 3
                        int count = count3 + 3;
                        int srcIndex = ctx.ReadWord();
                        if (srcIndex >= destIndex)
                            throw new NotImplementedException(string.Format("srcIndex >= destIndex {0} {1}", srcIndex, destIndex));

                        for (int end = destIndex + count; destIndex < end; destIndex++)
                            dest[destIndex] = dest[srcIndex++];
                    }
                }
            }
        }

        public static byte[] Encode(byte[] src)
        {
            /* quick & dirty format80 encoder -- only uses raw copy operator, terminated with a zero-run. */
            /* this does not produce good compression, but it's valid format80 */

            var ctx = new FastByteReader(src);
            var ms = new MemoryStream();

            do
            {
                var len = Math.Min(ctx.Remaining(), 0x3F);
                ms.WriteByte((byte)(0x80 | len));
                while (len-- > 0)
                    ms.WriteByte(ctx.ReadByte());
            }
            while (!ctx.Done());

            ms.WriteByte(0x80);	// terminator -- 0-length run.

            return ms.ToArray();
        }
        public static unsafe uint DecodeInto(byte* src, byte* dest)
        {
            byte* pdest = dest;
            byte* readp = src;
            byte* writep = dest;

            while (true)
            {
                byte code = *readp++;
                byte* copyp;
                int count;
                if ((~code & 0x80) != 0)
                {
                    //bit 7 = 0
                    //command 0 (0cccpppp p): copy
                    count = (code >> 4) + 3;
                    copyp = writep - (((code & 0xf) << 8) + *readp++);
                    while (count-- != 0)
                        *writep++ = *copyp++;
                }
                else
                {
                    //bit 7 = 1
                    count = code & 0x3f;
                    if ((~code & 0x40) != 0)
                    {
                        //bit 6 = 0
                        if (count == 0)
                            //end of image
                            break;
                        //command 1 (10cccccc): copy
                        while (count-- != 0)
                            *writep++ = *readp++;
                    }
                    else
                    {
                        //bit 6 = 1
                        if (count < 0x3e)
                        {
                            //command 2 (11cccccc p p): copy
                            count += 3;
                            copyp = &pdest[*(ushort*)readp];

                            readp += 2;
                            while (count-- != 0)
                                *writep++ = *copyp++;
                        }
                        else if (count == 0x3e)
                        {
                            //command 3 (11111110 c c v): fill
                            count = *(ushort*)readp;
                            readp += 2;
                            code = *readp++;
                            while (count-- != 0)
                                *writep++ = code;
                        }
                        else
                        {
                            //command 4 (copy 11111111 c c p p): copy
                            count = *(ushort*)readp;
                            readp += 2;
                            copyp = &pdest[*(ushort*)readp];
                            readp += 2;
                            while (count-- != 0)
                                *writep++ = *copyp++;
                        }
                    }
                }
            }

            return (uint)(dest - pdest);
        }
    }

}
