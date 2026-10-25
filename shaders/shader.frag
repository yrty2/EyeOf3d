#version 450
#extension GL_GOOGLE_include_directive : require
#include "f.glsl"

layout(push_constant) uniform Uniforms{
  float time;
  float wheel;
  float far;
  vec2 mouse;
  vec2 resolution;
  vec4 offset;
  vec4 cameraQuat;
  vec4 device1;
}uni;
layout(binding=0) uniform sampler2D tadokorokouji;
layout(location=0) out vec4 outColor;
const vec3 baseColor=vec3(0.18,0.65,0.95);
const vec3 directionalLight=vec3(-0.5,0.8,-0.3);
const vec3 rimColor=vec3(0.15, 0.75, 1.0);
const float RANGE=1.;
const int STEPS=256;
const int NORMAL_QUO=64;
const float goldencos=-.7373688782616119;
const float goldensin=.675490294061441;
vec2 uniformer(vec2 val){return (2.*val-uni.resolution)/uni.resolution.y;}
vec3 parrete(vec3 p){return 0.5+0.5*cos(uni.time+p+vec3(0.0,2.0,4.0));}
bool inside(vec3 p,inout vec3 c){return cube(p,RANGE) && f(exp(uni.wheel)*p+uni.offset.xyz,vec3(uni.mouse,uni.device1.w),uni.time,c);}
bool clipAxis(float ro,float rd,inout float tNear,inout float tFar){
  float a=(-RANGE-ro)/rd;
  float b=(RANGE-ro)/rd;
  if(a>b){
    float tmp=a;a=b;b=tmp;
  }
  tNear=max(tNear,a);
  tFar=min(tFar,b);
  return tNear<=tFar;
}
float rayHit(vec3 ro,vec3 rd,inout vec3 c){
  float tNear=-1e3;
  float tFar=1e3;
  if(!clipAxis(ro.x,rd.x,tNear,tFar) || !clipAxis(ro.y,rd.y,tNear,tFar) || !clipAxis(ro.z,rd.z,tNear,tFar))return -1.;
  float t0=max(tNear,0.);
  if(tFar<=t0)return -1.;
  if(inside(ro+rd*t0,c))return t0;
  float dt=(tFar-t0)/float(STEPS);
  float prevT=t0;
  for(int i=1; i<=STEPS; i++){
    float t=t0+dt*float(i);
    if(inside(ro+rd*t,c)){
      float lo=prevT;float hi=t;
      for(int j=0; j<8; j++){float mid=0.5*(lo+hi);if(inside(ro+rd*mid,c)){hi=mid;}else{lo=mid;}}
      return hi;
    }
    prevT=t;
  }
  return -1.;
}
vec3 getNormal(vec3 p,vec3 rd){
    vec3 zero=vec3(0);
    const float eps=0.008;
    vec3 sum=vec3(0);
    vec2 G=vec2(1.,0.);
    for(int i=0; i<NORMAL_QUO; i++){
      float z=1.-(2.*float(i)+1.)/float(NORMAL_QUO);
      vec3 d=vec3(sqrt(max(0.,1.-z*z))*G,z);
      G=vec2(G.x*goldencos-G.y*goldensin,G.y*goldencos+G.x*goldensin);
      sum+=d*(inside(p+eps*d,zero)?1.:0.);
    }
    float n2=dot(sum,sum);
    if(n2<1e-8)return -normalize(rd);
    return -sum*inversesqrt(n2);
}
void main(){vec2 uv=conj(uniformer(gl_FragCoord.xy));
  vec3 ro,rd,bs;
  ro=rotate(vec3(0.,0.,-uni.far*(1.-uni.offset.w)),uni.cameraQuat);
  rd=normalize(rotate(vec3(uv,1.0),uni.cameraQuat));
  bs=baseColor;
  float t=rayHit(ro,rd,bs);
  if(t>=0.){
    vec3 p,n,ld,effects;
    p=ro+rd*t;n=getNormal(p,rd);ld=normalize(rotate(directionalLight,uni.cameraQuat));
    effects.x=max(dot(n,ld),0.);
    effects.yz=vec2(effects.x*pow(max(dot(n,normalize(ld-rd)),0.),48.),.3*pow(1.0-max(dot(n,-rd),0.),3.));
    outColor=vec4(bs*(0.22+0.78*effects.x)+effects.y+rimColor*effects.z,1.0);
  }
}