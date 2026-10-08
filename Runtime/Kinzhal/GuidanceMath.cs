using System;
namespace Kinzhal
{
    public static class GuidanceMath
    {
        // Position takes priority near impact. Proportional navigation operates on
        // relative velocity, so a steep dive is retained without imposing a final angle.
        public static void TerminalAcceleration(double x,double y,double vx,double vy,double targetVx,double targetVy,out double ax,out double ay)
        {
            double range=Math.Max(1,Math.Sqrt(x*x+y*y)),lx=x/range,ly=y/range;
            double rx=targetVx-vx,ry=targetVy-vy,closing=Math.Max(0,-rx*lx-ry*ly);
            double radial=rx*lx+ry*ly;
            ax=4*closing*(rx-radial*lx)/range;
            ay=4*closing*(ry-radial*ly)/range;
            // Cancel only the gravity component normal to the current flight path.
            double speed2=Math.Max(1,vx*vx+vy*vy);
            ax-=9.81*vy*vx/speed2;
            ay+=9.81*(1-vy*vy/speed2);
        }
        public static double BoostPitch(double altitude,double verticalSpeed,double burn,double deltaV,double apex)
        {
            if(burn<.1||deltaV<1)return 0;
            double acceleration=deltaV/burn,low=-.35,high=.85;
            for(int i=0;i<28;i++){
                double fraction=(low+high)*.5;
                if(PredictedApex(altitude,verticalSpeed,burn,acceleration*fraction)>apex)high=fraction;else low=fraction;
            }
            return Math.Asin((low+high)*.5);
        }
        public static double PredictedApex(double altitude,double verticalSpeed,double burn,double upwardThrustAcceleration)
        {
            double endSpeed=verticalSpeed+(upwardThrustAcceleration-9.81)*burn;
            return altitude+verticalSpeed*burn+.5*(upwardThrustAcceleration-9.81)*burn*burn+Math.Max(0,endSpeed)*Math.Max(0,endSpeed)/19.62;
        }
        // Zero-effort miss including gravity, computed from velocity rather than nose direction.
        public static void CoastAcceleration(double x,double y,double vx,double vy,double targetVx,double targetVy,double time,double angleWeight,out double ax,out double ay)
        {
            time=Math.Max(.4,time);
            double rx=x+targetVx*time,ry=y+targetVy*time,speed=Math.Sqrt(vx*vx+vy*vy);
            double pursuitX=3*(rx-vx*time)/(time*time);
            double pursuitY=3*(ry-vy*time+4.905*time*time)/(time*time);
            double impactVx=speed*Math.Cos(55*Math.PI/180),impactVy=-speed*Math.Sin(55*Math.PI/180);
            double angledX=6*rx/(time*time)-(4*vx+2*impactVx)/time;
            double angledY=6*ry/(time*time)-(4*vy+2*impactVy)/time+9.81;
            angleWeight=Math.Max(0,Math.Min(1,angleWeight));
            ax=pursuitX+(angledX-pursuitX)*angleWeight;
            ay=pursuitY+(angledY-pursuitY)*angleWeight;
        }
    }
}
