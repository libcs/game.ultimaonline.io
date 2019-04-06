using System;
using System.Collections;
using UltimaOnline;

namespace UltimaOnline.Mobiles
{
    public class SpawnerType
    {
        public static Type GetType(string name)
        {
            return ScriptCompiler.FindTypeByName(name);
        }
    }
}