#ifndef MIDORI_GLSL
#define MIDORI_GLSL

const int perlin_fractalize=5;
const float pi=2.*acos(0.);
const float e=exp(1.);

bool C(bool a,vec3 p,inout vec3 q){
  q=a?p:q;
  return a;
}
vec2 vecos(vec2 p){
  return vec2(cos(p.x),cos(p.y));
}
vec3 vecos(vec3 p){
  return vec3(cos(p.x),cos(p.y),cos(p.z));
}
float min(vec2 p){
  return min(p.x,p.y);
}
float min(vec3 p){
  return min(p.x,min(p.y,p.z));
}
float max(vec2 p){
  return max(p.x,p.y);
}
float max(vec3 p){
  return max(p.x,max(p.y,p.z));
}
bool honeycomb(vec3 p){
  return 0.9<min(vecos(2.*pi*p.xy)) || 0.9<min(vecos(2.*pi*p.yz)) || 0.9<min(vecos(2.*pi*p.zx));
}
bool honeycomb2(vec3 p){
  return -1.8>cos(2.*pi*p.x)+cos(2.*pi*p.y) || -1.8>cos(2.*pi*p.y)+cos(2.*pi*p.z) || -1.8>cos(2.*pi*p.z)+cos(2.*pi*p.x);
}
bool tiny(float x){
  return abs(x)<0.1;
}
bool honeycomb2d(vec3 p){
  return -0.9>min(vecos(2.*pi*p.xy)) && tiny(p.z);
}
bool sphere(vec3 p,float r){
  return dot(p,p)<=r*r;
}
bool cube(vec3 p,float r){
  return max(abs(p.x),max(abs(p.y),abs(p.z)))<=r;
}
bool octagon(vec3 p,float r){
  return abs(p.x)+abs(p.y)+abs(p.z)<=r;
}
vec2 polar(float x){
  return vec2(cos(x),sin(x));
}
vec3 polar(vec2 z){
  return vec3(cos(z.x)*cos(z.y),sin(z.x)*cos(z.y),sin(z.y));
}
float sdot(vec2 p){
  return dot(p,p);
}
float sdot(vec3 p){
  return dot(p,p);
}
float sdot(vec4 p){
  return dot(p,p);
}
float arg(vec2 p){
  return atan(p.y,p.x);
}
vec2 arg(vec3 p){
  float r=length(p);
  if(r==0.){
    return vec2(0);
  }
  return vec2(arg(p.xy),asin(p.z/r));
}
vec3 exp3(vec3 p){
  return exp(p.x)*polar(p.yz);
}
vec3 ln(vec3 p){
  return vec3(0.5*log(dot(p,p)),arg(p));
}
vec3 mul(vec3 p,vec3 q){
  return vec3(p.x*q.x-dot(p.yz,q.yz),p.x*q.y+p.y*q.x,p.z*(q.x+q.y)+q.z*(p.x+p.y));
}
vec3 pow(vec3 p, float exponent){
  return pow(length(p), exponent)*polar(exponent*arg(p));
}
vec3 pow3(vec3 p,vec3 q){
  return exp3(mul(q,ln(p)));
}
vec2 conj(vec2 p){
  return vec2(p.x,-p.y);
}
vec3 conj(vec3 p){
  return vec3(p.x,-p.yz);
}
vec3 div(vec3 p,vec3 q){
  return mul(p,conj(q))/sdot(q);
}
vec3 cos3(vec3 p){
  vec3 ep=exp3(p);
  return 0.5*(ep+conj(ep)/sdot(p));
}
vec3 arccos(vec3 p){
  return mul(vec3(0.,-1.,0.),ln(p+pow(mul(p,p)-vec3(1.,0.,0.),0.5)));
}
vec4 mul(vec4 p,vec4 q){
  return vec4(p.w*q.xyz+p.xyz*q.w+cross(p.xyz, q.xyz),p.w*q.w-dot(p.xyz, q.xyz));
}
vec4 sphericalMover(vec2 p){
    float l=length(p);
    float s=l>1e-6?sin(l)/l:1.;
    return vec4(-p.y*s,p.x*s,0.,cos(l));
}
vec4 conj(vec4 p){
  return vec4(-p.xyz,p.w);
}
vec3 rotate(vec3 p, vec4 rotor){
  return mul(mul(rotor,vec4(p,0.)),conj(rotor)).xyz;
}
bool formula(float z,float a){
  return tiny(a-z);
}
uint hash(int p,int seed) {
    uint h=(uint(p)*0x8da6b343u)^(uint(seed)*0x165667b1u);
    h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;
    return h;
}
uint hash(ivec2 p,int seed) {
    uint h=(uint(p.x)*0x8da6b343u)^(uint(p.y)*0xd8163841u)^(uint(seed)*0x165667b1u);
    h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;
    return h;
}
uint hash(ivec3 p,int seed){
  uint h=(uint(p.x)*0x8da6b343u)^(uint(p.y)*0xd8163841u)^(uint(p.z)*0xcb1ab31fu)^(uint(seed)*0x165667b1u);
  h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;
  return h;
}
uint hash(ivec4 p,int seed) {
    uint h=(uint(p.x)*0x8da6b343u)^(uint(p.y)*0xd8163841u)^(uint(p.z)*0xcb1ab31fu)^(uint(p.w)*0x9e3779b9u)^(uint(seed)*0x165667b1u);
    h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;
    return h;
}
float noiseGrad(int i,float d,int seed){
    return (hash(i,seed)&1u)==0u?d:-d;
}
float noiseGrad(ivec2 face,vec2 d,int seed){
  int h=int(hash(face,seed)&7u);
  if(h<4){return ((h&1)==0?1.:-1.)*(h<2?d.x:d.y);}
  return (((h&1)==0?d.x:-d.x)+((h&1)==0?d.y:-d.y))*.70710678118;
}
float noiseGrad(ivec3 cell,vec3 d,int seed){
  int h=int(hash(cell,seed)&15u);
  vec2 u=vec2(h<8?d.x:d.y,h<4?d.y:(h==12 || h==14)?d.x:d.z);
  return ((h&1)==0?u.x:-u.x)+((h&2)==0?u.y:-u.y);
}
float noiseGrad(ivec4 hypercell,vec4 d,int seed){
  int h=int(hash(hypercell,seed)&31u);
  vec3 u=vec3(h<24?d.x:d.y,h<16?d.y:d.z,h<8?d.z:d.w);
  return ((h&1)==0?u.x:-u.x)+((h&2)==0?u.y:-u.y)+((h&4)==0?u.z:-u.z);
}
float scalarField(float p,int seed){
    int i=int(floor(p));
    float f=fract(p);
    return mix(noiseGrad(i,f,seed),noiseGrad(i+1,f-1.,seed),f*f*f*(f*(f*6.-15.)+10.));
}
float scalarField(vec2 p,int seed){
    ivec2 i=ivec2(floor(p));
    vec2 f=fract(p);
    vec2 u=f*f*f*(f*(f*6.-15.)+10.);
    float n00=noiseGrad(i+ivec2(0,0),f-vec2(0,0),seed);
    float n10=noiseGrad(i+ivec2(1,0),f-vec2(1,0),seed);
    float n01=noiseGrad(i+ivec2(0,1),f-vec2(0,1),seed);
    float n11=noiseGrad(i+ivec2(1,1),f-vec2(1,1),seed);
    float x0=mix(n00,n10,u.x);
    float x1=mix(n01,n11,u.x);
    return mix(x0,x1,u.y);
}
float scalarField(vec3 p,int seed){
    ivec3 i=ivec3(floor(p));
    vec3 f=fract(p);
    vec3 u=f*f*f*(f*(f*6.-15.)+10.);
    float n000=noiseGrad(i+ivec3(0,0,0),f-vec3(0,0,0),seed);
    float n100=noiseGrad(i+ivec3(1,0,0),f-vec3(1,0,0),seed);
    float n010=noiseGrad(i+ivec3(0,1,0),f-vec3(0,1,0),seed);
    float n110=noiseGrad(i+ivec3(1,1,0),f-vec3(1,1,0),seed);
    float n001=noiseGrad(i+ivec3(0,0,1),f-vec3(0,0,1),seed);
    float n101=noiseGrad(i+ivec3(1,0,1),f-vec3(1,0,1),seed);
    float n011=noiseGrad(i+ivec3(0,1,1),f-vec3(0,1,1),seed);
    float n111=noiseGrad(i+ivec3(1,1,1),f-vec3(1,1,1),seed);
    float x00=mix(n000,n100,u.x);
    float x10=mix(n010,n110,u.x);
    float x01=mix(n001,n101,u.x);
    float x11=mix(n011,n111,u.x);
    return mix(mix(x00,x10,u.y),mix(x01,x11,u.y),u.z);
}
float scalarField(vec4 p,int seed){
    ivec4 i=ivec4(floor(p));
    vec4 f=fract(p);
    vec4 u=f*f*f*(f*(f*6.-15.)+10.);
    float n0000=noiseGrad(i+ivec4(0,0,0,0),f-vec4(0,0,0,0),seed);
    float n1000=noiseGrad(i+ivec4(1,0,0,0),f-vec4(1,0,0,0),seed);
    float n0100=noiseGrad(i+ivec4(0,1,0,0),f-vec4(0,1,0,0),seed);
    float n1100=noiseGrad(i+ivec4(1,1,0,0),f-vec4(1,1,0,0),seed);
    float n0010=noiseGrad(i+ivec4(0,0,1,0),f-vec4(0,0,1,0),seed);
    float n1010=noiseGrad(i+ivec4(1,0,1,0),f-vec4(1,0,1,0),seed);
    float n0110=noiseGrad(i+ivec4(0,1,1,0),f-vec4(0,1,1,0),seed);
    float n1110=noiseGrad(i+ivec4(1,1,1,0),f-vec4(1,1,1,0),seed);
    float n0001=noiseGrad(i+ivec4(0,0,0,1),f-vec4(0,0,0,1),seed);
    float n1001=noiseGrad(i+ivec4(1,0,0,1),f-vec4(1,0,0,1),seed);
    float n0101=noiseGrad(i+ivec4(0,1,0,1),f-vec4(0,1,0,1),seed);
    float n1101=noiseGrad(i+ivec4(1,1,0,1),f-vec4(1,1,0,1),seed);
    float n0011=noiseGrad(i+ivec4(0,0,1,1),f-vec4(0,0,1,1),seed);
    float n1011=noiseGrad(i+ivec4(1,0,1,1),f-vec4(1,0,1,1),seed);
    float n0111=noiseGrad(i+ivec4(0,1,1,1),f-vec4(0,1,1,1),seed);
    float n1111=noiseGrad(i+ivec4(1,1,1,1),f-vec4(1,1,1,1),seed);
    float x000=mix(n0000,n1000,u.x);
    float x100=mix(n0100,n1100,u.x);
    float x010=mix(n0010,n1010,u.x);
    float x110=mix(n0110,n1110,u.x);
    float x001=mix(n0001,n1001,u.x);
    float x101=mix(n0101,n1101,u.x);
    float x011=mix(n0011,n1011,u.x);
    float x111=mix(n0111,n1111,u.x);
    float y00=mix(x000,x100,u.y);
    float y10=mix(x010,x110,u.y);
    float y01=mix(x001,x101,u.y);
    float y11=mix(x011,x111,u.y);
    return mix(mix(y00,y10,u.z),mix(y01,y11,u.z),u.w);
}
float fractalScalarField(float p,int seed){
  float value=0.;
  float z=1.;
  for(int k=0; k<perlin_fractalize; ++k){
    value+=scalarField(p*z,seed)/z;
    z*=2.;
  }
  return .5*value;
}
float fractalScalarField(vec2 p,int seed){
  float value=0.;
  float z=1.;
  for(int k=0; k<perlin_fractalize; ++k){
    value+=scalarField(p*z,seed)/z;
    z*=2.;
  }
  return .5*value;
}
float fractalScalarField(vec3 p,int seed){
  float value=0.;
  float z=1.;
  for(int k=0; k<perlin_fractalize; ++k){
    value+=scalarField(p*z,seed)/z;
    z*=2.;
  }
  return .5*value;
}
float fractalScalarField(vec4 p,int seed){
  float value=0.;
  float z=1.;
  for(int k=0; k<perlin_fractalize; ++k){
    value+=scalarField(p*z,seed)/z;
    z*=2.;
  }
  return .5*value;
}
#endif