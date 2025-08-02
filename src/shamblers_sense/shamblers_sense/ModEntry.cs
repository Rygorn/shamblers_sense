using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Shamblers_Sense
{
    public class ModEntry : IModApi
    {
        public void InitMod(Mod mod)
        {
            ShamblersSenseLogger.Log("ModEntry.InitMod() called");
            ShamblersSenseMain.Init();
            ShamblersSenseLogger.Log("ShamblersSenseMain.Init() finished");
        }
    }
}