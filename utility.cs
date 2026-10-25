using System;
using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Input;
using System.Runtime.InteropServices;

namespace util;

public class Game{
    public static float time=0f;
    public static float far=1.3f;
    public static float zpos=0f;
    public static float wheel=(float)Math.Log(10);
    public static Vector2 mouse=new Vector2(0,0);
    public static Vector3 offset=new Vector3(0,0,0);
    public static Vector4 CameraQuat=new Vector4(0,0,0,1);
    public static bool fpscam=false;
    public static uint unibufferSize=80;
    public static Uniforms getCurrent(Extent2D swapchainExtent)=>new(){
        time=Game.time,
        wheel=Game.wheel,
        mouse=Game.mouse,
        far=Game.far,
        resolution=new(swapchainExtent.Width,swapchainExtent.Height),
        offset=new Vector4(Game.offset.X,Game.offset.Y,Game.offset.Z,fpscam?1f:0f),
        cameraQuat=CameraQuat,
        device1=new Vector4(0f,0f,0f,zpos)
    };
    public static Vector2 lastPosition;
    public static void mouseEvent(Silk.NET.Input.IMouse mouse){
        mouse.Scroll+=(_,scroll)=>{
            if(mouse.IsButtonPressed(MouseButton.Right)){
                Game.zpos+=scroll.Y/10;
            }else{
                wheel-=scroll.Y/10;
            }
        };
        lastPosition=mouse.Position;
        mouse.MouseMove+=(_, position)=>{
            Vector2 delta=position-lastPosition;
            lastPosition=position;
            if(mouse.IsButtonPressed(MouseButton.Left)){
                float val=(float)Math.PI/(1600);
                CameraQuat=mul(CameraQuat,sphericalMover(val*delta));
            }
            if(mouse.IsButtonPressed(MouseButton.Right)){
                Game.mouse+=delta*0.01f;
            }
        };
    }
    public static void Process(IInputContext input){
        Vector3 move=new Vector3(0,0,0);
        if(input.Keyboards[0].IsKeyPressed(Key.W)){
            move.Z++;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.S)){
            move.Z--;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.A)){
            move.X--;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.D)){
            move.X++;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.Space)){
            move.Y++;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.ShiftLeft)){
            move.Y--;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.R)){
            offset=new Vector3(0,0,0);
        }
        if(input.Keyboards[0].IsKeyPressed(Key.Q)){
            CameraQuat=new Vector4(0,0,0,1);
        }
        if(input.Keyboards[0].IsKeyPressed(Key.E)){
            zpos=0;
            mouse=Vector2.Zero;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.F)){
            fpscam=!fpscam;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.P)){
            far/=1.01f;
        }
        if(input.Keyboards[0].IsKeyPressed(Key.O)){
            far*=1.01f;
        }
        float s=move.Length();
        if(s>0){
            offset+=(float)Math.Exp(wheel-5)*rot(move,CameraQuat)/s;
        }
    }
    public static Vector4 sphericalMover(Vector2 p){
        float l=p.Length();
        if(l<0.001){
            return new(0,0,0,1);
        }
        float s=(float)Math.Sin(l)/l;
        return new(-p.Y*s,-p.X*s,0,(float)Math.Cos(l));
    }
    public static Vector4 mul(Vector4 a,Vector4 b)=>new Vector4(
        a.W*b.X+a.X*b.W+a.Y*b.Z-a.Z*b.Y,
        a.W*b.Y+a.Y*b.W+a.Z*b.X-a.X*b.Z,
        a.W*b.Z+a.Z*b.W+a.X*b.Y-a.Y*b.X,
        a.W*b.W-a.X*b.X-a.Y*b.Y-a.Z*b.Z
    );
    public static Vector3 rot(Vector3 p,Vector4 q){
        Vector4 p2=mul(mul(q,new Vector4(p,0)),new Vector4(-q.X,-q.Y,-q.Z,q.W));
        return new Vector3(p2.X,p2.Y,p2.Z);
    }
}

[StructLayout(LayoutKind.Explicit,Size=80)]
public struct Uniforms{
    [FieldOffset(0)]
    public float time;
    [FieldOffset(4)]
    public float wheel;
    [FieldOffset(8)]
    public float far;
    [FieldOffset(16)]
    public Vector2 mouse;
    [FieldOffset(24)]
    public Vector2 resolution;
    [FieldOffset(32)]
    public Vector4 offset;
    [FieldOffset(48)]
    public Vector4 cameraQuat;
    [FieldOffset(64)]
    public Vector4 device1;
}