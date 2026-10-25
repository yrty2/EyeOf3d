#include "midori.glsl"
#ifndef FUNCTION
#define FUNCTION
bool f(vec3 p,vec3 v,float t,inout vec3 c){
  /*if(p.z<0.){
    return false;
  }*/
  /*vec4 q=vec4(p.yz,0.,p.x);
  vec4 V=vec4(v.yz,0.,v.x);
  float k=0.;
  vec4 prev=q;
  while(k<30){
    q=mul(q,q)+V;
    if(sdot(q)>4.)return true;
    prev=q;
    k++;
  }
  if(k==30.){
    c=.5*normalize(prev.xyz)+vec3(.5);
  }
  return false;*/
  //return honeycomb(exp3(p));
  //return formula(p.y,p.z*p.x);
  /*float size=15.;
  vec3 q=floor(p);
  return size*scalarField(q.xy/(3.5*size),10)+fractalScalarField(q.xy/size,20)*size<q.z;*/
  return length(vec3(scalarField(p,20),scalarField(p,30),scalarField(p,40)))>0.2;
}
#endif