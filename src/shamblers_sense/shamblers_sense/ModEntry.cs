using ModApi;

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