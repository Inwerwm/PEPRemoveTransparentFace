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

                    // テクスチャが読めなければcontinue
                    if (texturePath.Length == 0)
                    {
                        errorLog.Add(material, "材質にテクスチャが設定されていません。");
                        continue;
                    }
                    if (string.Compare(Path.GetExtension(texturePath), ".dds", true) == 0)
                    {
                        errorLog.Add(material, "ddsファイルは未対応です。");
                        continue;
                    }

                    // テクスチャ画像を読み込み
                    using (Image textureImage = (string.Compare(Path.GetExtension(texturePath), ".tga", true) == 0) ? TgaDecoder.TgaDecoder.FromFile(texturePath) : Image.FromFile(texturePath))
                    using (Bitmap textureBitmap = new Bitmap(textureImage))
                    {
                        // ビットマップをロック
                        var textureBitmapData = textureBitmap.LockBits(new Rectangle(0, 0, textureBitmap.Width, textureBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        var pixels = new byte[textureBitmap.Width * textureBitmap.Height * 4];
                        Marshal.Copy(textureBitmapData.Scan0, pixels, 0, pixels.Length);

                        // UVMapを確実に開放するためtry-catch-finallyを使う
                        try
                        {
                            var removeFaceList = new List<IPXFace>();

                            // 各面が不透明なピクセルを持つかを調べる
                            foreach (var face in material.Faces)
                            {
                                var boundByRatio = face.ComputeUVBoundingBox().ToRectangle();
                                var boundByPixel = new Rectangle((boundByRatio.X * textureBitmap.Width).Round(), (boundByRatio.Y * textureBitmap.Height).Round(), (boundByRatio.Width * textureBitmap.Width).Round(), (boundByRatio.Height * textureBitmap.Height).Round());

                                // 境界領域内のピクセルを走査
                                bool existOpacityPixel = false;
                                for (int y = boundByPixel.Top; y < boundByPixel.Bottom; y++)
                                {
                                    for (int x = boundByPixel.Left; x < boundByPixel.Right; x++)
                                    {
                                        int index = (x + y * textureBitmap.Width) * 4;
                                        // 0.5を足してピクセルの中心座標を現在位置にする
                                        // PMXのUVは割合表現で管理されているためテクスチャ画像の大きさで割って割合表現化
                                        V2 currentPosition = new V2((x + 0.5f) / textureBitmap.Width, (y + 0.5f) / textureBitmap.Height);

                                        if (face.UVIsInclude(currentPosition))
                                            existOpacityPixel |= pixels[index + 3] != 0;

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

                            // 透明面を材質から除去
                            foreach (var face in removeFaceList)
                            {
                                material.Faces.Remove(face);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            textureBitmap.UnlockBits(textureBitmapData);
                        }
                    }
                }

                Utility.Update(args.Host.Connector, pmx);

                if (errorLog.Any())
                    MessageBox.Show(errorLog.Aggregate($"完了{Environment.NewLine}以下の材質は無視されました：", (msg, pair) => $"{msg}{Environment.NewLine}\t{pair.Key.Name}:{pair.Value}"), "選択材質の透明面を除去", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                else
                    MessageBox.Show("完了", "選択材質の透明面を除去", MessageBoxButtons.OK, MessageBoxIcon.Information);

            }
            catch (Exception ex)
            {
                Utility.ShowException(ex);
            }
        }
    }
}
