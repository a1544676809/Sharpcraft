// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace Sharpcraft.Client.Tests.Render;

public sealed class ZZZScaleDbg
{
    private readonly ITestOutputHelper _o; public ZZZScaleDbg(ITestOutputHelper o)=>_o=o;
    [Fact] public void Diag(){
        _o.WriteLine($"Illager Scale={IllagerModelDefinition.Vindicator.Scale} Raw={IllagerModelDefinition.Vindicator.RawSourceFrame}");
        var atlas=ItemAtlas.FromTextures(new Dictionary<string,TextureData>(StringComparer.Ordinal){["entity/illager/vindicator"]=new(64,64,new byte[64*64*4])});
        var head=IllagerModelDefinition.Vindicator.Parts.Single(p=>p.Name=="head");
        var single=new EntityModelDefinition("x",64,64,[head],texturePath:"entity/illager/vindicator",scale:IllagerModelDefinition.Vindicator.Scale,baselinePx:0,rawSourceFrame:true);
        _o.WriteLine($"single Scale={single.Scale}");
        var g=new EntityModelMesher().BuildModel(single,new EntityRenderState(0,0,0,0,0,180f,0f),atlas.Get("entity/illager/vindicator")!,64,64,modelScale:1f);
        var p=VertexFieldReader.Position(g.Vertices,0);
        _o.WriteLine($"our head v0=({p.X:F6},{p.Y:F6},{p.Z:F6}) sample=(0.234375,-0.492125,0.234375)");
        Assert.True(true);
    }
}
