using System;
namespace Kinzhal
{
    // A medium altitude arch with a 55 degree final tangent. This is a guidance reference,
    // not a claim about the trajectory achievable by the game's aerodynamic model.
    public static class LoftProfile
    {
        public static double Ceiling(double startHeight,double targetHeight,double range)
            =>Math.Max(startHeight+1500,targetHeight+Clamp(16000+Math.Max(0,range-80000)*.035+Math.Max(0,startHeight-targetHeight)*.25,16000,25000));
        public static double TerminalRun(double ceiling,double targetHeight,double range)
            =>Clamp((ceiling-targetHeight)/Math.Tan(55*Math.PI/180),range*.035,range*.40);
        public static void Sample(double distance,double range,double startHeight,double targetHeight,out double height,out double slope)
        {
            range=Math.Max(1,range);
            // Cubic control points are above the actual peak. Solve them so that
            // the reference arch reaches the requested apex, not merely its ceiling.
            double top=ControlHeight(startHeight,targetHeight,Ceiling(startHeight,targetHeight,range)),x1=range*.25,x2=range-TerminalRun(top,targetHeight,range);
            double low=0,high=1;
            distance=Clamp(distance,0,range);
            for(int i=0;i<24;i++){double mid=(low+high)*.5;if(Cubic(0,x1,x2,range,mid)<distance)low=mid;else high=mid;}
            double t=(low+high)*.5;
            height=Cubic(startHeight,top,top,targetHeight,t);
            slope=Derivative(startHeight,top,top,targetHeight,t)/Math.Max(.001,Derivative(0,x1,x2,range,t));
        }
        private static double ControlHeight(double start,double end,double apex)
        {
            double low=apex,high=apex+(apex-Math.Min(start,end))*2;
            for(int i=0;i<32;i++){
                double mid=(low+high)*.5;
                double a=Math.Sqrt(mid-start),b=Math.Sqrt(mid-end);
                double t=a/(a+b);
                if(Cubic(start,mid,mid,end,t)<apex)low=mid;else high=mid;
            }
            return (low+high)*.5;
        }
        private static double Cubic(double a,double b,double c,double d,double t){double u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        private static double Derivative(double a,double b,double c,double d,double t){double u=1-t;return 3*u*u*(b-a)+6*u*t*(c-b)+3*t*t*(d-c);}
        private static double Clamp(double value,double min,double max)=>Math.Max(min,Math.Min(max,value));
    }
}
