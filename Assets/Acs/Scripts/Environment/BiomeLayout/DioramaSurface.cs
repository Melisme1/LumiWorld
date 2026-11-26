using System;
using System.Collections.Generic;
using static LumiWorld.Acs.DioramaPlan;

namespace LumiWorld.Acs
{
    // Sample the actual triangles baked by HexBiomeSurfaceMeshBuilder, including stream slope and bank bevels.
    public static class DioramaSurface
    {
        public struct Vertex { public float x,y,z; }
        public sealed class Geometry { public Vertex[] vertices; public int[] indices; }
        const float Step=.075f;
        public static Surface Sample(Geometry land,Geometry water)
        {
            float xmin=float.MaxValue,zmin=float.MaxValue,xmax=float.MinValue,zmax=float.MinValue;
            foreach(var v in land.vertices){xmin=Math.Min(xmin,v.x);xmax=Math.Max(xmax,v.x);zmin=Math.Min(zmin,v.z);zmax=Math.Max(zmax,v.z);}
            int width=(int)Math.Ceiling((xmax-xmin)/Step)+3,height=(int)Math.Ceiling((zmax-zmin)/Step)+3;
            var g=Raster(land,xmin-Step,zmin-Step,width,height);
            var w=Raster(water,xmin-Step,zmin-Step,width,height);
            var edge=new float[g.Length];var bank=new float[g.Length];var dry=new float[g.Length];
            for(int i=0;i<g.Length;i++)
            {
                bool wet=w[i]>g[i]+.001f;
                edge[i]=float.IsNegativeInfinity(g[i])?0:100;
                bank[i]=wet?0:100;
                dry[i]=!wet?0:100;
            }
            DistanceField(edge,width,height);DistanceField(bank,width,height);DistanceField(dry,width,height);
            var cells=new List<Cell>();
            for(int z=1;z<height-1;z++) for(int x=1;x<width-1;x++)
            {
                int i=z*width+x;
                if(edge[i]<Step*.99f)continue;
                float dx=bank[i-1]-bank[i+1],dz=bank[i-width]-bank[i+width];
                bool wet=bank[i]==0;
                cells.Add(new Cell{x=xmin+(x-1)*Step,z=zmin+(z-1)*Step,groundY=g[i],waterY=wet?w[i]:g[i],wet=wet,
                    edge=edge[i],shore=wet?dry[i]:bank[i],waterAngle=(float)(Math.Atan2(-dz,dx)*180/Math.PI)});
            }
            return new Surface{cells=cells,minX=xmin,minZ=zmin,width=xmax-xmin,length=zmax-zmin};
        }
        static float[] Raster(Geometry mesh,float xmin,float zmin,int width,int height)
        {
            var y=new float[width*height];
            for(int i=0;i<y.Length;i++) y[i]=float.NegativeInfinity;
            if(mesh==null)return y;
            for(int i=0;i<mesh.indices.Length;i+=3)
            {
                var a=mesh.vertices[mesh.indices[i]];var b=mesh.vertices[mesh.indices[i+1]];var c=mesh.vertices[mesh.indices[i+2]];
                float bx=b.x-a.x,bz=b.z-a.z,cx=c.x-a.x,cz=c.z-a.z,det=bx*cz-bz*cx;
                if(Math.Abs(det)<1e-9f)continue;
                int x0=Math.Max(0,(int)Math.Floor((Math.Min(a.x,Math.Min(b.x,c.x))-xmin)/Step));
                int x1=Math.Min(width-1,(int)Math.Ceiling((Math.Max(a.x,Math.Max(b.x,c.x))-xmin)/Step));
                int z0=Math.Max(0,(int)Math.Floor((Math.Min(a.z,Math.Min(b.z,c.z))-zmin)/Step));
                int z1=Math.Min(height-1,(int)Math.Ceiling((Math.Max(a.z,Math.Max(b.z,c.z))-zmin)/Step));
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    float rx=xmin+x*Step-a.x,rz=zmin+z*Step-a.z;
                    float u=(rx*cz-rz*cx)/det,v=(bx*rz-bz*rx)/det;
                    if(u>=-.0001f && v>=-.0001f && u+v<=1.0001f)
                        y[z*width+x]=Math.Max(y[z*width+x],a.y+u*(b.y-a.y)+v*(c.y-a.y));
                }
            }
            return y;
        }
        static void DistanceField(float[] field,int width,int height)
        {
            for(int pass=0;pass<2;pass++)
            {
                int d=pass==0?1:-1;
                for(int j=0;j<field.Length;j++)
                {
                    int i=d==1?j:field.Length-1-j, x=i%width,z=i/width;
                    if(x-d>=0 && x-d<width)field[i]=Math.Min(field[i],field[i-d]+Step);
                    if(z-d<0 || z-d>=height)continue;
                    int prev=i-width*d;
                    field[i]=Math.Min(field[i],field[prev]+Step);
                    if(x>0)field[i]=Math.Min(field[i],field[prev-1]+Step*1.414214f);
                    if(x+1<width)field[i]=Math.Min(field[i],field[prev+1]+Step*1.414214f);
                }
            }
        }
        public struct Spot { public float x,y,z,depth,clearance; public bool wet; }
        // Keep a continuous dry corridor at the requested diameter, including after manually moving rocks.
        static HashSet<(int,int)> LargestWalkableRegion(Surface surface,List<Prop> props,float radius)
        {
            var remaining=new HashSet<(int,int)>();
            foreach(var c in surface.cells)
            {
                if(c.wet || c.shore<radius || c.edge<radius) continue;
                if(props.Exists(p=>p.radius>0 && Dist(c.x,c.z,p.x,p.z)<radius+p.radius)) continue;
                remaining.Add(GridKey(surface,c));
            }
            var largest=new HashSet<(int,int)>();
            var queue=new Queue<(int,int)>();
            while(remaining.Count>0)
            {
                var region=new HashSet<(int,int)>();
                (int,int) start=default;
                foreach(var point in remaining){start=point;break;}
                remaining.Remove(start);
                queue.Enqueue(start);
                while(queue.Count>0)
                {
                    var p=queue.Dequeue();
                    region.Add(p);
                    Enqueue(p.Item1+1,p.Item2); Enqueue(p.Item1-1,p.Item2);
                    Enqueue(p.Item1,p.Item2+1); Enqueue(p.Item1,p.Item2-1);
                }
                if(region.Count>largest.Count) largest=region;
            }
            return largest;
            void Enqueue(int x,int z){if(remaining.Remove((x,z)))queue.Enqueue((x,z));}
        }
        static (int,int) GridKey(Surface s,Cell c)=>((int)Math.Round((c.x-s.minX)/Step),(int)Math.Round((c.z-s.minZ)/Step));
        public static List<Spot> Spots(Surface surface,List<Prop> props,float radius,float walkSpacing,float swimSpacing,float minDepth,float dryCorridorWidth=0)
        {
            var spots=new List<Spot>();
            var connected=dryCorridorWidth>0?LargestWalkableRegion(surface,props,Math.Max(radius,dryCorridorWidth*.5f)):null;
            foreach(var c in surface.cells)
            {
                if(!c.wet && connected!=null && !connected.Contains(GridKey(surface,c)))continue;
                float depth=c.waterY-c.groundY;
                if(c.wet && depth<minDepth)continue;
                float clearance=Math.Min(c.edge,c.shore);
                foreach(var p in props)if(p.radius>0)
                    clearance=Math.Min(clearance,(float)Dist(c.x,c.z,p.x,p.z)-p.radius);
                if(clearance<radius)continue;
                if(spots.Exists(s=>s.wet==c.wet && Dist(s.x,s.z,c.x,c.z)<(c.wet?swimSpacing:walkSpacing)))continue;
                spots.Add(new Spot{x=c.x,y=c.wet?c.waterY:c.groundY,z=c.z,depth=depth,clearance=clearance,wet=c.wet});
            }
            return spots;
        }
    }
}
