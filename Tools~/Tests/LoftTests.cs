using System;
using Kinzhal;
static class LoftTests
{
    static int checks;
    static void Require(bool condition,string label){checks++;if(!condition)throw new Exception(label);}
    static double TerminalMiss(double x,double height,double vx,double vy,double targetSpeed)
    {
        double px=0,py=height,tx=x,actualX=0,actualY=0;
        const double dt=.002;
        for(int step=0;step<20000;step++){
            GuidanceMath.TerminalAcceleration(tx-px,-py,vx,vy,targetSpeed,0,out var ax,out var ay);
            double speed2=vx*vx+vy*vy,along=(ax*vx+ay*vy)/speed2;
            ax-=along*vx;ay-=along*vy;
            double magnitude=Math.Sqrt(ax*ax+ay*ay),limit=Math.Min(1,78.48/Math.Max(.001,magnitude));
            actualX+=(ax*limit-actualX)*dt/.12;actualY+=(ay*limit-actualY)*dt/.12;
            double oldX=px,oldY=py;
            vx+=actualX*dt;vy+=(actualY-9.81)*dt;
            px+=vx*dt;py+=vy*dt;tx+=targetSpeed*dt;
            if(py<=0){double fraction=oldY/(oldY-py);return Math.Abs(oldX+(px-oldX)*fraction-(tx-targetSpeed*dt+targetSpeed*dt*fraction));}
        }
        return double.PositiveInfinity;
    }
    static void Main()
    {
        double lateMiss=TerminalMiss(425,368,1079,-1007,0); Require(lateMiss>20&&lateMiss<30,"Late correction remains bounded by 8 g; cannot repair a late 30 m miss");
        Require(TerminalMiss(12358,7039,1423,-382,0)<10,"Recorded 14 km approach converges before ground impact with actuator lag");
        Require(TerminalMiss(26500,9537,1460,-164,0)<10,"Early terminal correction avoids overshoot from shallow dive");
        Require(TerminalMiss(26500,9537,1460,-164,20)<10,"Moving target terminal intercept includes relative velocity");
        Require(TerminalMiss(26500,10537,1460,-164,0)<10,"Terminal guidance converges with an additional 1 km height margin");
        double boostPitch=GuidanceMath.BoostPitch(8000,300,15,800,17000);
        Require(Math.Abs(GuidanceMath.PredictedApex(8000,300,15,800/15d*Math.Sin(boostPitch))-17000)<.01,"Boost accounts for future coast height");
        Require(GuidanceMath.BoostPitch(18000,650,10,400,17000)<0,"Already excessive climb commands lower powered pitch");
        GuidanceMath.CoastAcceleration(60000,-12000,1400,600,0,0,45,0,out var earlyX,out var earlyY);
        double dot=(earlyX*1400+earlyY*600)/(1400*1400+600*600);
        Require(earlyY-dot*600<0,"Rising warhead corrects downward before target is close");
        double t=30,vx=1400,vy=-400;
        GuidanceMath.CoastAcceleration(vx*t,vy*t-4.905*t*t,vx,vy,0,0,t,0,out var onCourseX,out var onCourseY);
        Require(Math.Abs(onCourseX)<.001&&Math.Abs(onCourseY)<.001,"Correct ballistic intercept does not receive artificial correction");
        GuidanceMath.CoastAcceleration(60000,-12000,1400,600,0,0,45,.5,out var shapedX,out var shapedY);
        Require(double.IsFinite(shapedX)&&double.IsFinite(shapedY),"Impact-angle shaping is finite");
        foreach(double altitude in new[]{0d,1000,8000,12000})
        foreach(double target in new[]{0d,500,2000})
        foreach(double range in new[]{48000d,50000,80000,150000,360000}){
            double ceiling=LoftProfile.Ceiling(altitude,target,range);
            LoftProfile.Sample(0,range,altitude,target,out var launch,out _);
            LoftProfile.Sample(range,range,altitude,target,out var impact,out var tangent);
            Require(Math.Abs(launch-altitude)<.01,"Launch endpoint");
            Require(Math.Abs(impact-target)<.01,"Target endpoint");
            double angle=Math.Atan(-tangent)*180/Math.PI;
            Require(angle>=50&&angle<=60,"Steep final tangent");
            double peak=0;
            for(int step=0;step<=200;step++){
                LoftProfile.Sample(range*step/200,range,altitude,target,out var height,out var slope);
                Require(double.IsFinite(height)&&double.IsFinite(slope),"Finite profile");
                Require(height<=ceiling+.01&&height>=Math.Min(altitude,target)-.01,"Bounded arch");
                peak=Math.Max(peak,height);
            }
            Require(peak>=ceiling-10,"Arch actually reaches requested apex");
            Require(ceiling>=15000,"Minimum 15 km reference apex");
        }
        Require(LaunchEnvelope.MaximumRange(0,250)==50000,"Sea-level range");
        Require(LaunchEnvelope.MaximumRange(8000,250)==189000,"8 km range");
        Require(LaunchEnvelope.MaximumRange(10000,250)==360000,"10 km range");
        foreach(float speed in new[]{150f,250,400}){
            float previous=0;
            for(int altitude=0;altitude<=20000;altitude+=250){
                float range=LaunchEnvelope.MaximumRange(altitude,speed);
                Require(range>=previous&&range<=360000,"Range increases with altitude and remains capped");previous=range;
            }
        }
        LoftProfile.Sample(80000,80000,8000,0,out _,out var finalSlope);
        Console.WriteLine($"PASS {checks} profile checks; 80 km/8 km reference ceiling={LoftProfile.Ceiling(8000,0,80000):0}m, final tangent={Math.Atan(-finalSlope)*180/Math.PI:0.0}deg. Native mission physics are not simulated.");
    }
}
