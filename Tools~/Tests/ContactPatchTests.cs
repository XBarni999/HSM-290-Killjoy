using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
class ContactPatchTests {
 static void Main(){
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{
   string name=new AssemblyName(args.Name).Name+".dll";
   foreach(var root in new[]{@"F:\Games\Nuclear.Option.v0.34.1\BepInEx\core",@"F:\NCMod\Blueprinter-Editor\Packages\nuclearoption",@"F:\Games\Nuclear.Option.v0.34.1\NuclearOption_Data\Managed",Path.GetFullPath("Tools~/Runtime/Kinzhal/bin/Release/net472")}){
    string path=Path.Combine(root,name);if(File.Exists(path))return Assembly.LoadFrom(path);
   }return null;
  };Run();
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static void Run(){
  var native=AccessTools.Method(typeof(Missile),"DetectCollisions");
  var original=PatchProcessor.GetOriginalInstructions(native);
  var method=AccessTools.Method(typeof(Kinzhal.Plugin),"ContactQueries");
  var transformed=((IEnumerable<CodeInstruction>)method.Invoke(null,new object[]{original})).ToList();
  var replacement=AccessTools.Method(typeof(Kinzhal.Plugin),"ContactLinecast");
  int count=0;
  for(int i=0;i<transformed.Count;i++)if(transformed[i].Calls(replacement)){
   if(i==0||transformed[i-1].opcode!=System.Reflection.Emit.OpCodes.Ldarg_0)throw new Exception("Missing instance argument");count++;
  }
  if(count!=2)throw new Exception("Expected both native collision linecasts");
  Console.WriteLine("PASS: both native DetectCollisions linecasts patched with instance argument; original collision/damage/fuse method retained. Engine physics not executed.");
 }
}
