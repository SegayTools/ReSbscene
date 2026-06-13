using System.Text.Json;
using SbScene.Core.Output;
using SbScene.Core.Rendering;
using SbScene.Core.Resources;
using SbScene.Core.Semantics;
using SbScene.Core.Unity;
using SbScene.Core.Vtbf;

namespace SbScene.Core.Tests;

public sealed class UnityNavicharaExporterTests
{
    [Fact]
    public void ExportBuildsCoreClipCurvesAndDeduplicatedUnityPaths()
    {
        var scene = Scene(
            [Node(0, "Part"), Node(1, "Part")],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 20)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        Assert.False(result.Failed);
        Assert.Contains(result.Export.Nodes, node => node.UnityPath == "Null_UI_Navichara_27/MoveObject/Part__n0");
        Assert.Contains(result.Export.Nodes, node => node.UnityPath == "Null_UI_Navichara_27/MoveObject/Part__n1");
        var clip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Default"));
        var curve = Assert.Single(clip.Curves);
        Assert.Equal("RectTransform", curve.Unity.Component);
        Assert.Equal("m_AnchoredPosition.x", curve.Unity.Property);
        Assert.Equal([0, 10], curve.Keys.Select(key => key.Frame).ToArray());
        Assert.Equal(20, curve.Keys[^1].Value, precision: 6);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PlaceholderClip" && diagnostic.TargetClip == "Navi_Welcom");
    }

    [Fact]
    public void ExportConvertsSceneYDownValuesToUnityYUpCoordinates()
    {
        var scene = Scene(
            [Node(0, "Part", translationX: 10, translationY: 15, rotationZ: 30)],
            [ImageCast(castIndex: 0, width: 100, height: 80, pivotX: 25, pivotY: 20)],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0,
                    Track(1, Key(0, 4), Key(10, -8)),
                    Track(5, Key(0, 30), Key(10, -15)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        var node = Assert.Single(result.Export.Nodes.Where(node => node.Id == 0));
        Assert.Equal(10, node.Static.AnchoredPosition.X, precision: 6);
        Assert.Equal(-15, node.Static.AnchoredPosition.Y, precision: 6);
        Assert.Equal(30, node.Static.RotationZ, precision: 6);
        Assert.Equal(25, node.Static.PivotPixels.X, precision: 6);
        Assert.Equal(60, node.Static.PivotPixels.Y, precision: 6);
        Assert.Equal(0.25, node.Static.PivotNormalized.X, precision: 6);
        Assert.Equal(0.75, node.Static.PivotNormalized.Y, precision: 6);

        var clip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Default"));
        var translateY = Assert.Single(clip.Curves.Where(curve => curve.SbsceneTrackType == 1));
        Assert.Equal("m_AnchoredPosition.y", translateY.Unity.Property);
        Assert.Equal(-4, translateY.Keys[0].Value, precision: 6);
        Assert.Equal(8, translateY.Keys[^1].Value, precision: 6);
        var rotateZ = Assert.Single(clip.Curves.Where(curve => curve.SbsceneTrackType == 5));
        Assert.Equal("localEulerAnglesRaw.z", rotateZ.Unity.Property);
        Assert.Equal(30, rotateZ.Keys[0].Value, precision: 6);
        Assert.Equal(-15, rotateZ.Keys[^1].Value, precision: 6);
    }

    [Fact]
    public void BuildProfileTemplateOmitsAnimationsNodeAndKeepsCandidateClipMapping()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 15,
                Motion(0, Track(1, Key(0, 0), Key(15, 5)))));

        var template = UnityNavicharaExporter.BuildProfileTemplate(scene);
        var json = JsonSerializer.Serialize(template, SbSceneJson.CreateOptions(indented: true));
        using var document = JsonDocument.Parse(json);

        var slot = Assert.Single(template.Clips["Navi_Default"].SourceSlots);
        Assert.Equal("Action_Wait1", slot.Animation);
        Assert.False(document.RootElement.TryGetProperty("animations", out _));
    }

    [Fact]
    public void BuildProfileTemplateOutputsEmptyCommonBaseSourceSlots()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 15,
                Motion(0, Track(1, Key(0, 0), Key(15, 5)))));

        var template = UnityNavicharaExporter.BuildProfileTemplate(scene);
        var json = JsonSerializer.Serialize(template, SbSceneJson.CreateOptions(indented: true));
        using var document = JsonDocument.Parse(json);
        var commonBaseSourceSlots = document.RootElement.GetProperty("commonBaseSourceSlots");

        Assert.Empty(template.CommonBaseSourceSlots);
        Assert.Equal(JsonValueKind.Array, commonBaseSourceSlots.ValueKind);
        Assert.Equal(0, commonBaseSourceSlots.GetArrayLength());
    }

    [Fact]
    public void BuildProfileTemplateDoesNotInferCurveSlotRepeatFromTargetLoop()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 15,
                Motion(0, Track(1, Key(0, 0), Key(15, 5)))));

        var template = UnityNavicharaExporter.BuildProfileTemplate(scene);

        var slot = Assert.Single(template.Clips["Navi_Default"].SourceSlots);
        Assert.Equal("Action_Wait1", slot.Animation);
        Assert.Equal("curve", slot.Frame);
        Assert.False(slot.Repeat ?? true);
    }

    [Fact]
    public void ProfileLoaderAcceptsAutoMaxDurationFrames()
    {
        using var temp = new TemporaryDirectory();
        var path = System.IO.Path.Combine(temp.Path, "profile.json");
        File.WriteAllText(
            path,
            """
            {
              "settings": {
                "pixelsPerUnit": 1,
                "curveBakeMode": "keyed"
              },
              "clips": {
                "Navi_Default": {
                  "loop": true,
                  "durationFrames": "autoMax",
                  "sourceSlots": [
                    { "animation": "Action_Wait1", "frame": "curve", "repeat": true }
                  ]
                }
              }
            }
            """);

        var profile = UnityNavicharaProfileLoader.Load(path);

        var clip = Assert.Single(profile.Clips);
        Assert.Equal("autoMax", clip.Value.DurationFrames);
        Assert.True(clip.Value.Loop);
    }

    [Fact]
    public void ExportBakesSampledCurvesWhenProfileRequestsIt()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(1, Key(0, 0), Key(10, 10)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Settings = new UnityNavicharaProfileSettings
            {
                CurveBakeMode = "sampled60",
            },
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
            });

        Assert.Equal("sampled60", result.Export.Settings.CurveBakeMode);
        var curve = Assert.Single(result.Export.Clips.Single(clip => clip.Name == "Navi_Default").Curves);
        Assert.Equal(11, curve.Keys.Count);
        Assert.Equal(0, curve.Keys[0].Frame);
        Assert.Equal(10, curve.Keys[^1].Frame);
    }

    [Fact]
    public void ExportAppendsMapSourceSlotWithoutForcingTargetLoopRepeat()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 10)))),
            Animation(
                "Action_Wait2",
                endFrame: 10,
                Motion(0, Track(1, Key(0, 0), Key(10, 30)))),
            Animation(
                "Action_Override",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 5), Key(10, 25)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait2",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                Maps =
                [
                    new UnityNavicharaAnimationMap("Action_Override", "Navi_Default"),
                ],
            });

        var clip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Default"));
        Assert.Equal(3, clip.SourceSlots.Count);
        Assert.Equal("Action_Override", clip.SourceSlots[^1].Animation);
        Assert.False(clip.SourceSlots[^1].Repeat ?? true);
        Assert.Equal(2, clip.Curves.Count);
        Assert.Contains(clip.Curves, curve => curve.SbsceneTrackType == 1);
        var overrideCurve = Assert.Single(clip.Curves.Where(curve => curve.SbsceneTrackType == 0));
        Assert.Equal(25, overrideCurve.Keys[^1].Value, precision: 6);
    }

    [Fact]
    public void ExportPreservesHermiteTangentPresenceFlags()
    {
        var scene = Scene(
            [Node(0, "Root")],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(1,
                    Key(0, 0, interpolation: 2, tangentOut: 1),
                    Key(10, 10, interpolation: 2, tangentIn: 1)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
            });

        var curve = Assert.Single(result.Export.Clips.Single(clip => clip.Name == "Navi_Default").Curves);
        Assert.All(curve.Keys, key => Assert.True(key.HasInTangent || key.HasOutTangent));
        Assert.True(curve.Keys[0].HasOutTangent);
        Assert.True(curve.Keys[1].HasInTangent);
    }

    [Fact]
    public void ExportMarksAdditiveImageCasts()
    {
        var scene = Scene(
            [Node(0, "Heart")],
            [ImageCast(castIndex: 0, width: 63, height: 62, pivotX: 31, pivotY: 31, imageCastFlags: 1)],
            Animation(
                "Action_Joy3",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 0)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Fun_Start"] = new UnityNavicharaProfileClip
                {
                    Loop = false,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Joy3",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        var node = Assert.Single(result.Export.Nodes.Where(node => node.Id == 0));
        Assert.NotNull(node.Image);
        Assert.Equal(1, node.Image.DrawMode);
        Assert.True(node.Image.AdditiveBlend);
    }

    [Fact]
    public void ExportPreservesImageCastUvTransformFlags()
    {
        const int uvMode = 2;
        var flags = SbSceneImageCastConventions.HorizontalFlipMask
            | SbSceneImageCastConventions.VerticalFlipMask
            | (uvMode << 6);
        var scene = Scene(
            [Node(0, "Base")],
            [ImageCast(castIndex: 0, width: 96, height: 94, pivotX: 48, pivotY: 47, imageCastFlags: flags)],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 0)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        var node = Assert.Single(result.Export.Nodes.Where(node => node.Id == 0));
        Assert.NotNull(node.Image);
        Assert.True(node.Image.FlipX);
        Assert.True(node.Image.FlipY);
        Assert.Equal(uvMode, node.Image.UvMode);
    }

    [Fact]
    public void ExportCombinesMaterialAndIlluminationColorIntoGraphicColor()
    {
        var scene = Scene(
            [Node(0, "Heart", materialColor: "#FF000000", illuminationColor: "#FF000000")],
            [ImageCast(castIndex: 0, width: 63, height: 62, pivotX: 31, pivotY: 31)],
            Animation(
                "Action_Joy3",
                endFrame: 10,
                Motion(0,
                    Track(21, Key(0, 0), Key(10, 0)),
                    Track(22, Key(0, 0), Key(10, 0)),
                    Track(23, Key(0, 0), Key(10, 0)),
                    Track(24, Key(0, 1), Key(10, 1)),
                    Track(25, Key(0, 0.875), Key(10, 0.875)),
                    Track(26, Key(0, 0), Key(10, 0)),
                    Track(27, Key(0, 1), Key(10, 1)),
                    Track(28, Key(0, 1), Key(10, 1)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Fun_Start"] = new UnityNavicharaProfileClip
                {
                    Loop = false,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Joy3",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "UnsupportedIlluminationTrack");
        var clip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Fun_Start"));
        var colorCurves = clip.Curves.Where(curve => curve.Unity.Property.StartsWith("m_Color.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, colorCurves.Length);
        Assert.Equal(223.0 / 255.0, Assert.Single(colorCurves.Where(curve => curve.Unity.Property == "m_Color.r")).Keys[0].Value, precision: 6);
        Assert.Equal(0, Assert.Single(colorCurves.Where(curve => curve.Unity.Property == "m_Color.g")).Keys[0].Value, precision: 6);
        Assert.Equal(1, Assert.Single(colorCurves.Where(curve => curve.Unity.Property == "m_Color.b")).Keys[0].Value, precision: 6);
        var alphaCurve = Assert.Single(clip.Curves.Where(curve => curve.Unity.Property == "m_Alpha"));
        Assert.Equal("CanvasGroup", alphaCurve.Unity.Component);
        Assert.Equal(1, alphaCurve.Keys[0].Value, precision: 6);
    }

    [Fact]
    public void ExportAutoCentersVisibleContentToOriginByDefault()
    {
        // Node offset far from origin with a centered 100x80 image cast.
        var scene = Scene(
            [Node(0, "Body", translationX: 200, translationY: 120)],
            [RenderableImageCast(castIndex: 0, width: 100, height: 80, pivotX: 50, pivotY: 40)],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 0)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = CenterTestProfile();

        var centered = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        // Scene-space content center is the node world translation (200, 120) for a centered sprite.
        // Unity offset cancels X and flips Y: offset = (-200, +120).
        var offset = centered.Export.Settings.RootTransform.Offset;
        Assert.Equal(-200, offset.X, precision: 3);
        Assert.Equal(120, offset.Y, precision: 3);
        Assert.Contains(centered.Diagnostics, diagnostic => diagnostic.Code == "AutoCenterApplied");

        // Applying offset to the node's Unity position must land the content at the origin.
        var node = Assert.Single(centered.Export.Nodes.Where(node => node.Id == 0));
        Assert.Equal(0, node.Static.AnchoredPosition.X + offset.X, precision: 3);
        Assert.Equal(0, node.Static.AnchoredPosition.Y + offset.Y, precision: 3);
    }

    [Fact]
    public void ExportKeepsRawCoordinatesWhenAutoCenterDisabled()
    {
        var scene = Scene(
            [Node(0, "Body", translationX: 200, translationY: 120)],
            [RenderableImageCast(castIndex: 0, width: 100, height: 80, pivotX: 50, pivotY: 40)],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 0)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = CenterTestProfile(),
                AllowPlaceholderClips = true,
                AutoCenter = false,
            });

        var offset = result.Export.Settings.RootTransform.Offset;
        Assert.Equal(0, offset.X, precision: 6);
        Assert.Equal(0, offset.Y, precision: 6);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "AutoCenterApplied");
    }

    [Fact]
    public void ExportDoesNotOverrideExplicitProfileOffset()
    {
        var scene = Scene(
            [Node(0, "Body", translationX: 200, translationY: 120)],
            [RenderableImageCast(castIndex: 0, width: 100, height: 80, pivotX: 50, pivotY: 40)],
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 0)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            Settings = new UnityNavicharaProfileSettings
            {
                RootTransform = new UnityNavicharaRootTransform
                {
                    Scale = 1.0,
                    Offset = new UnityNavicharaVector2 { X = 5, Y = -7 },
                },
            },
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
            });

        var offset = result.Export.Settings.RootTransform.Offset;
        Assert.Equal(5, offset.X, precision: 6);
        Assert.Equal(-7, offset.Y, precision: 6);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AutoCenterSkippedExplicitOffset");
    }

    private static UnityNavicharaExportProfile CenterTestProfile()
    {
        return new UnityNavicharaExportProfile
        {
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot
                        {
                            Animation = "Action_Wait1",
                            Frame = "curve",
                        },
                    ],
                },
            },
        };
    }

    [Fact]
    public void CommonBaseSlotsBakeIntoStaticBindAndMergeIntoEveryClip()
    {
        // Node 0 bind display = true. Change_Fashion has a Display(type 11) track that hides it at frame >= 1.
        // Node 1 bind display = true, untouched by Change_Fashion.
        var scene = Scene(
            [Node(0, "uniform_part"), Node(1, "plain_part")],
            Animation(
                "Change_Fashion",
                endFrame: 3,
                Motion(0, Track(11, Key(0, 1), Key(1, 0), Key(3, 0)))),
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(1, Track(0, Key(0, 0), Key(10, 5)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            CommonBaseSourceSlots =
            [
                new UnityNavicharaSourceSlot { Animation = "Change_Fashion", Frame = "3", Repeat = false },
            ],
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        new UnityNavicharaSourceSlot { Animation = "Action_Wait1", Frame = "curve" },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
                AutoCenter = false,
            });

        // Static bind: node 0 hidden by Change_Fashion:3, node 1 stays visible.
        var node0 = Assert.Single(result.Export.Nodes.Where(node => node.Id == 0));
        Assert.False(node0.Static.Display);
        var node1 = Assert.Single(result.Export.Nodes.Where(node => node.Id == 1));
        Assert.True(node1.Static.Display);

        // Every core clip's sourceSlots starts with the common-base Change_Fashion:3 slot.
        Assert.All(result.Export.Clips, clip =>
        {
            Assert.NotEmpty(clip.SourceSlots);
            Assert.Equal("Change_Fashion", clip.SourceSlots[0].Animation);
            Assert.Equal("3", clip.SourceSlots[0].Frame);
        });

        // The explicit clip slot follows the common-base slot.
        var defaultClip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Default"));
        Assert.Equal(2, defaultClip.SourceSlots.Count);
        Assert.Equal("Action_Wait1", defaultClip.SourceSlots[1].Animation);
    }

    [Fact]
    public void CommonBaseSlotIsNotDuplicatedWhenClipAlreadyReferencesSameAnimation()
    {
        var scene = Scene(
            [Node(0, "part")],
            Animation(
                "Change_Fashion",
                endFrame: 3,
                Motion(0, Track(11, Key(0, 1), Key(3, 0)))),
            Animation(
                "Action_Wait1",
                endFrame: 10,
                Motion(0, Track(0, Key(0, 0), Key(10, 5)))));
        using var temp = new TemporaryDirectory();
        var sbscenePath = Path.Combine(temp.Path, "test.sbscene");
        File.WriteAllText(sbscenePath, "hash-source");
        var profile = new UnityNavicharaExportProfile
        {
            CommonBaseSourceSlots =
            [
                new UnityNavicharaSourceSlot { Animation = "Change_Fashion", Frame = "3", Repeat = false },
            ],
            Clips =
            {
                ["Navi_Default"] = new UnityNavicharaProfileClip
                {
                    Loop = true,
                    DurationFrames = "autoMax",
                    SourceSlots =
                    [
                        // Clip explicitly overrides Change_Fashion to a different frame.
                        new UnityNavicharaSourceSlot { Animation = "Change_Fashion", Frame = "1" },
                        new UnityNavicharaSourceSlot { Animation = "Action_Wait1", Frame = "curve" },
                    ],
                },
            },
        };

        var result = UnityNavicharaExporter.Export(
            scene,
            sbscenePath,
            "test.svo",
            temp.Path,
            new UnityNavicharaExportOptions
            {
                CharacterId = 27,
                Profile = profile,
                AllowPlaceholderClips = true,
                AutoCenter = false,
            });

        var defaultClip = Assert.Single(result.Export.Clips.Where(clip => clip.Name == "Navi_Default"));
        // Common-base Change_Fashion is skipped because the clip already references it; explicit frame wins.
        Assert.Single(defaultClip.SourceSlots.Where(slot => slot.Animation == "Change_Fashion"));
        Assert.Equal("1", defaultClip.SourceSlots.Single(slot => slot.Animation == "Change_Fashion").Frame);
    }

    [Fact]
    public void ProfileLoaderReadsCommonBaseSourceSlots()
    {
        using var temp = new TemporaryDirectory();
        var path = System.IO.Path.Combine(temp.Path, "profile.json");
        File.WriteAllText(
            path,
            """
            {
              "settings": { "pixelsPerUnit": 1 },
              "commonBaseSourceSlots": [
                { "animation": "Change_Fashion", "frame": "3", "repeat": false }
              ],
              "clips": {
                "Navi_Default": {
                  "loop": true,
                  "sourceSlots": [
                    { "animation": "Action_Wait1", "frame": "curve" }
                  ]
                }
              }
            }
            """);

        var profile = UnityNavicharaProfileLoader.Load(path);

        var slot = Assert.Single(profile.CommonBaseSourceSlots);
        Assert.Equal("Change_Fashion", slot.Animation);
        Assert.Equal("3", slot.Frame);
        Assert.False(slot.Repeat ?? true);
    }

    private static SbSceneFile Scene(IReadOnlyList<NodeInfo> nodes, params AnimationInfo[] animations)
    {
        return Scene(nodes, [], animations);
    }

    private static SbSceneFile Scene(IReadOnlyList<NodeInfo> nodes, IReadOnlyList<SbSceneImageCast> imageCasts, params AnimationInfo[] animations)
    {
        return new SbSceneFile
        {
            SourcePath = "test.sbscene",
            SourceSize = 0,
            Vtbf = new VtbfDocument
            {
                Magic = "VTBF",
                Length = 0,
                Blocks = [],
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
            Surfboard = new SurfboardModel
            {
                Objects = [],
                Nodes = nodes,
                Transform2DRecords = nodes.Select(node => node.Transform2D!).ToArray(),
                NodeCategoryRecords = [],
                NodeCategoryDetails = [],
                NodeGroups = [],
                Resources = new SbSceneResourceMap
                {
                    Atlases = [],
                    ImageCasts = imageCasts,
                    CnumRecords = [],
                    CrfdRecords = [],
                    TextRecords = [],
                    SliceCasts = [],
                },
                Camera = null,
                Animations = animations,
                AnimationBindings = [],
                VariantHints = [],
                UnknownFields = [],
            },
            Summary = new ParseSummary
            {
                RootBlockCount = 0,
                TotalBlockCount = 0,
                NodeCount = nodes.Count,
                AnimationCount = animations.Length,
                VariantHintCount = 0,
                BlockCounts = new Dictionary<string, int>(),
                Warnings = [],
            },
        };
    }

    private static NodeInfo Node(
        int index,
        string name,
        double translationX = 0,
        double translationY = 0,
        double rotationZ = 0,
        string materialColor = "#FFFFFFFF",
        string illuminationColor = "#FF000000")
    {
        return new NodeInfo
        {
            Index = index,
            Offset = 0,
            Path = $"NODE[{index}]",
            Name = name,
            Flags = null,
            FlagBits = [],
            ChildIndex = null,
            SiblingIndex = null,
            Comment = null,
            CategoryId = null,
            Group = string.Empty,
            Transform2D = new Transform2DInfo
            {
                Index = index,
                Offset = 0,
                Path = $"TRS2[{index}]",
                Translation = new Vector2Value { X = (float)translationX, Y = (float)translationY },
                RotationZ = null,
                RotationZRaw = null,
                RotationZDegreesCandidate = rotationZ,
                Scale = new Vector2Value { X = 1, Y = 1 },
                Display = true,
                MaterialColor = Color(materialColor),
                IlluminationColor = Color(illuminationColor),
                VertexColors = [],
                MultiPosFlags = null,
                MultiSizeFlags = null,
                Fields = [],
            },
            HasTransform2 = true,
            HasTransform3 = false,
            HasData = false,
            HasCategory = false,
            StringFields = [],
            NumericFields = [],
            ChildTags = [],
        };
    }

    private static ColorArgbValue Color(string text)
    {
        var raw = text.TrimStart('#');
        var value = Convert.ToUInt32(raw, 16);
        return new ColorArgbValue
        {
            A = (byte)((value >> 24) & 0xFF),
            R = (byte)((value >> 16) & 0xFF),
            G = (byte)((value >> 8) & 0xFF),
            B = (byte)(value & 0xFF),
        };
    }

    private static SbSceneImageCast ImageCast(int castIndex, float width, float height, float pivotX, float pivotY, int imageCastFlags = 0)
    {
        return new SbSceneImageCast
        {
            Index = 0,
            Offset = 0,
            ImageCastFlags = imageCastFlags,
            ImageCastFlagBits = Enumerable.Range(0, 32)
                .Where(bit => ((uint)imageCastFlags & (1u << bit)) != 0)
                .ToArray(),
            CastIndex = castIndex,
            NodeName = "node",
            Width = width,
            Height = height,
            PivotX = pivotX,
            PivotY = pivotY,
            DeclaredCropReferenceCount = 0,
            PrimaryCropReferenceCount = 0,
            SecondaryCropReferenceCount = null,
            SecondaryCropFlag = null,
            PrimaryCropIndex = null,
            SecondaryCropIndex = null,
            PrimaryCropReferenceIndex = null,
            SecondaryCropReferenceIndex = null,
            CropReferenceCountMatches = true,
            CropIndexValues = [],
            CropRefCounts = [],
            PrimaryCropReferences = [],
            SecondaryCropReferences = [],
            CropReferences = [],
        };
    }

    private static SbSceneImageCast RenderableImageCast(int castIndex, float width, float height, float pivotX, float pivotY)
    {
        return new SbSceneImageCast
        {
            Index = 0,
            Offset = 0,
            ImageCastFlags = 0,
            ImageCastFlagBits = [],
            CastIndex = castIndex,
            NodeName = "node",
            Width = width,
            Height = height,
            PivotX = pivotX,
            PivotY = pivotY,
            DeclaredCropReferenceCount = 1,
            PrimaryCropReferenceCount = 1,
            SecondaryCropReferenceCount = null,
            SecondaryCropFlag = null,
            PrimaryCropIndex = null,
            SecondaryCropIndex = null,
            PrimaryCropReferenceIndex = 0,
            SecondaryCropReferenceIndex = null,
            CropReferenceCountMatches = true,
            CropIndexValues = [],
            CropRefCounts = [],
            PrimaryCropReferences = [CropReference(0)],
            SecondaryCropReferences = [],
            CropReferences = [CropReference(0)],
        };
    }

    private static SbSceneCropReference CropReference(int index)
    {
        return new SbSceneCropReference
        {
            Index = index,
            RawHex = string.Empty,
            Kind = 0,
            TextureListIndex = 0,
            TextureIndex = 0,
            CropIndex = index,
            AtlasName = null,
            CropPath = null,
        };
    }

    private static AnimationInfo Animation(string name, int endFrame, params MotionInfo[] motions)
    {
        return new AnimationInfo
        {
            Index = 0,
            Offset = 0,
            Path = "ANIM[0]",
            Name = name,
            StringFields = [],
            NumericFields =
            [
                NumericField("0x56", endFrame),
                NumericField("0x5F", 0),
            ],
            Motions = motions,
        };
    }

    private static MotionInfo Motion(int castIndex, params TrackInfo[] tracks)
    {
        return new MotionInfo
        {
            Index = 0,
            Offset = 0,
            Path = "MOT[0]",
            Name = null,
            TargetName = null,
            TargetIndex = null,
            CastIndex = castIndex,
            DeclaredTrackCount = tracks.Length,
            StringFields = [],
            NumericFields = [],
            Tracks = tracks,
        };
    }

    private static TrackInfo Track(int trackType, params KeyframeInfo[] keyframes)
    {
        return new TrackInfo
        {
            Index = 0,
            Offset = 0,
            Path = "TRK[0]",
            Name = null,
            TrackId = null,
            TrackType = trackType,
            TrackTypeName = null,
            ValueType = null,
            ValueTypeName = null,
            DeclaredKeyCountFromTrack = keyframes.Length,
            DeclaredKeyCountFromKeyBlock = keyframes.Length,
            KeyCountMatchesDeclaration = true,
            Flags = 0x13,
            KeyValueStorage = null,
            TargetIndex = null,
            FirstFrame = keyframes.Length > 0 ? keyframes[0].KeyFrame : null,
            LastFrame = keyframes.Length > 0 ? keyframes[^1].KeyFrame : null,
            DeclaredKeyCount = keyframes.Length,
            IsLikelyStateTrack = false,
            StringFields = [],
            NumericFields = [],
            Keyframes = keyframes,
        };
    }

    private static KeyframeInfo Key(int frame, double value)
    {
        return Key(frame, value, interpolation: 1);
    }

    private static KeyframeInfo Key(int frame, double value, int interpolation, double? tangentIn = null, double? tangentOut = null)
    {
        return new KeyframeInfo
        {
            Index = frame,
            Offset = 0,
            Path = $"KEY[{frame}]",
            Fields = [],
            KeyFrame = frame,
            ScalarValue = value,
            BoolValue = null,
            PackedAngleRaw = null,
            PackedAngleDegreesCandidate = null,
            KeyValueTypeHex = null,
            KeyValueTypeName = null,
            KeyValueKind = null,
            Interpolation = interpolation,
            InterpolationName = null,
            TangentIn = tangentIn,
            TangentOut = tangentOut,
            TimeCandidates = [],
            ValueCandidates = [value],
            Preview = null,
        };
    }

    private static FieldValueSummary NumericField(string idHex, int value)
    {
        return new FieldValueSummary
        {
            IdHex = idHex,
            TypeHex = "0x02",
            TypeName = "int",
            Preview = value.ToString(),
            Int64Values = [value],
            Float64Values = null,
            StringValue = null,
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sbscene-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
