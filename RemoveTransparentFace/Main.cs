using PEPExtensions;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoveTransparentFace
{
    public class RemoveTransparentFace : PEPluginClass
    {
        public RemoveTransparentFace() : base()
        {
        }

        public override string Name
        {
            get
            {
                return "選択材質の透明面を除去";
            }
        }

        public override string Version
        {
            get
            {
                return "0.0";
            }
        }

        public override string Description
        {
            get
            {
                return "選択材質の透明面を除去";
            }
        }

        public override IPEPluginOption Option
        {
            get
            {
                // boot時実行, プラグインメニューへの登録, メニュー登録名
                return new PEPluginOption(false, true, "選択材質の透明面を除去");
            }
        }

        public override void Run(IPERunArgs args)
        {
            try
            {
                var pmx = args.Host.Connector.Pmx.GetCurrentState();
                var selectedMaterials = args.Host.Connector.Form.GetSelectedMaterialIndices().Select(i => pmx.Material[i]);

                var errorLog = new Dictionary<IPXMaterial, string>();

                foreach (var material in selectedMaterials)
                {
                    string texturePath = material.Tex;

                    if (texturePath == "")
                    {
                        errorLog.Add(material, "材質にテクスチャが設定されていません。");
                        continue;
                    }

                    if (string.Compare(Path.GetExtension(texturePath), ".dds", true) == 0)
                    {
                        errorLog.Add(material, "ddsファイルは未対応です。");
                        continue;
                    }

                    // 削除対象のリスト
                    var removeFaceList = new List<IPXFace>();

                    // テクスチャ画像を読み込み
                    using (Image texture = (string.Compare(Path.GetExtension(texturePath), ".tga", true) == 0) ? TgaDecoder.TgaDecoder.FromFile(texturePath) : Image.FromFile(texturePath))
                    using (Bitmap UVMap = new Bitmap(texture))
                    {
                        // ビットマップをロック
                        var bmpData = UVMap.LockBits(new Rectangle(0, 0, UVMap.Width, UVMap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        var pixels = new byte[UVMap.Width * UVMap.Height * 4];
                        Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

                        // UVMapを確実に開放するためtry-catch-finallyを使う
                        try
                        {
                            // 各面が不透明なピクセルを持つかを調べる
                            foreach (var face in material.Faces)
                            {
                                var bb = face.ComputeUVBoundingBox().ToRectangle();
                                var pxBound = new Rectangle((bb.X * UVMap.Width).Round(), (bb.Y * UVMap.Height).Round(), (bb.Width * UVMap.Width).Round(), (bb.Height * UVMap.Height).Round());

                                Console.WriteLine(face.PrintUV());
                                // 境界領域内の全ピクセルを走査
                                bool existOpacityPixel = false;
                                for (int y = pxBound.Top; y < pxBound.Bottom; y++)
                                {
                                    var logList = new List<string>();

                                    for (int x = pxBound.Left; x < pxBound.Right; x++)
                                    {
                                        int index = (x + y * UVMap.Width) * 4;

                                        // 現在のピクセルが面の領域内であった場合
                                        V2 currentPosition = new V2((x + 0.5f) / UVMap.Width, (y + 0.5f) / UVMap.Height);
                                        if (face.UVIsInclude(currentPosition))
                                        {
                                            // ピクセルの透明度が0であるかを論理和で集計
                                            existOpacityPixel |= pixels[index + 3] != 0;
                                        }

                                        // 短絡評価
                                        if (existOpacityPixel)
                                            break;
                                    }

                                    // 短絡評価
                                    if (existOpacityPixel)
                                        break;
                                }
                                if (!existOpacityPixel)
                                    removeFaceList.Add(face);

                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            UVMap.UnlockBits(bmpData);
                        }

                    }

                    // 透明面を材質から除去
                    foreach (var face in removeFaceList)
                    {
                        material.Faces.Remove(face);
                    }
                }

                Utility.Update(args.Host.Connector, pmx);

                if (errorLog.Any())
                {
                    MessageBox.Show(errorLog.Aggregate($"完了{Environment.NewLine}以下の材質は無視されました：", (msg, pair) => $"{msg}{Environment.NewLine}\t{pair.Key.Name}:{pair.Value}"), "選択材質の透明面を除去", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else
                {
                    MessageBox.Show("完了", "選択材質の透明面を除去", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Utility.ShowException(ex);
            }
        }
    }
}
