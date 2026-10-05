using System;
namespace Kinzhal
{
    public static class LaunchEnvelope
    {
        public const float MinimumRange=48000;
        public static float MaximumRange(float altitude,float speed)
            =>Clamp(50000+139000*Clamp(altitude/8000,0,1)+171000*Clamp((altitude-8000)/2000,0,1)+10000*Clamp((speed-250)/250,-1,1),MinimumRange,360000);
        private static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));
    }
}
