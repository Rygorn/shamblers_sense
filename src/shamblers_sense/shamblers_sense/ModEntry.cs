using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Shamblers_Sense
{
    public class ModEntry : IModApi
    {
        public void InitMod(Mod mod)
        {
            ShamblersSenseMain.Init();
        }
    }
}