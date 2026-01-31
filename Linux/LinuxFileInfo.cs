using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace MandalaLogics.Path
{
    [Flags]
    public enum LinuxAccessBits : byte
    {
        None = 0,
        Read = 4,
        Write = 2,
        Execute = 1
    }

    public readonly struct LinuxFileInfo 
    {
        //INTERNAL STATIC
        internal static Regex lsRegex = new Regex(@"(?<dir>[dl-])(?<ownerbits>[-rwx]{3})(?<groupbits>[-rwx]{3})(?<otherbits>[-rwx]{3})\s+\d+\s+(?<username>\S+)\s+(?<groupname>\S+)\s+\d+\s+(?<date>\w{3}\s+\w{1,2}\s+[:\d]+)\s+(?<filename>.+)[\n$]");
        internal static Regex linkRegex = new Regex(@"(?<name>.*?)\s->\s(?<path>.+)$");

        //PUBLIC
        public int UID {get;}
        public int GID {get;}
        public string Name {get;}
        public string LinkPath {get;}
        public LinuxAccessBits OwnerBits {get;}
        public LinuxAccessBits GroupBits {get;}
        public LinuxAccessBits OtherBits {get;}
        public DestType Type {get;}

        //CONSTRUCTORS
        internal LinuxFileInfo(Match match)
        {
            switch (match.Groups[1].Value)
            {
                case "d":
                    Type = DestType.Dir;
                    break;
                case "l":
                    Type = DestType.SymLink;
                    break;
                case "-":
                    Type = DestType.File;
                    break;
                default:
                    Type = DestType.Unknown;
                    break;
            }

            OwnerBits = ParseAccessBits(match.Groups[2].Value);
            GroupBits = ParseAccessBits(match.Groups[3].Value);
            OtherBits = ParseAccessBits(match.Groups[4].Value);

            UID = int.Parse(match.Groups[5].Value);
            GID = int.Parse(match.Groups[6].Value);

            if (Type == DestType.SymLink)
            {
                var m = linkRegex.Match(match.Groups[8].Value);

                Name = m.Groups[1].Value;
                LinkPath = m.Groups[2].Value;
            }
            else
            {
                Name = match.Groups[8].Value;
                LinkPath = string.Empty;
            }
        }

        //METHODS
        public AccessLevel CalculateAccessLevel(int owner, int[] groups)
        {
            AccessLevel ret = AccessLevel.None;

            if (owner == UID)
            {
                ret |= OwnerBits.HasFlag(LinuxAccessBits.Read) ? AccessLevel.Read : AccessLevel.None;
                return ret | (OwnerBits.HasFlag(LinuxAccessBits.Write) ? AccessLevel.Write : AccessLevel.None);
            }
            else if (groups.Contains(GID))
            {
                ret |= GroupBits.HasFlag(LinuxAccessBits.Read) ? AccessLevel.Read : AccessLevel.None;
                return ret | (GroupBits.HasFlag(LinuxAccessBits.Write) ? AccessLevel.Write : AccessLevel.None);
            }
            else
            {
                ret |= OtherBits.HasFlag(LinuxAccessBits.Read) ? AccessLevel.Read : AccessLevel.None;
                return ret | (OtherBits.HasFlag(LinuxAccessBits.Write) ? AccessLevel.Write : AccessLevel.None);
            }
        }

        internal static LinuxAccessBits ParseAccessBits(string bits)
        {
            LinuxAccessBits ret = bits[0] == 'r' ?  LinuxAccessBits.Read : LinuxAccessBits.None;

            ret |= bits[1] == 'w' ?  LinuxAccessBits.Write : LinuxAccessBits.None;

            return ret | (bits[2] == 'x' ?  LinuxAccessBits.Execute : LinuxAccessBits.None);
        }
    }

}