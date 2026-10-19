using BarcodeStudio.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace BarcodeStudio.Services;
public static class LabelFactory
{
    public static FrameworkElement Build(Product product,string company,string phone,DateTime production,DateTime expiry,int days,double widthMm,double heightMm,bool showExpiry=true,double productFontSize=13)
    {
        var width=widthMm*96/25.4;var height=heightMm*96/25.4;
        var root=new Grid{Width=width,Height=height,Background=Brushes.White,FlowDirection=FlowDirection.RightToLeft};
        // Font sizes scale with label size, while maintaining legibility for typical 60x40 mm labels.
        var scale=Math.Min(width/227.0,height/151.0);
        var stack=new StackPanel{Margin=new Thickness(5*scale,3*scale,5*scale,2*scale),HorizontalAlignment=HorizontalAlignment.Stretch};
        TextBlock Txt(string text,double size,bool bold=false)
        {
            return new TextBlock {Text=text,FontFamily=new FontFamily("Arial"),FontSize=size*scale,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,Foreground=Brushes.Black,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,FlowDirection=FlowDirection.RightToLeft,MaxHeight=size*scale*1.35};
        }
        stack.Children.Add(Txt(company,15,true));
        if(!string.IsNullOrWhiteSpace(phone))stack.Children.Add(Txt(phone,10,true));
        var barcode=BarcodeBitmap(product.Code);
        var barcodeImg=new Image{Source=barcode,Height=43*scale,Stretch=Stretch.Fill,Margin=new Thickness(13*scale,2*scale,13*scale,0)};
        RenderOptions.SetBitmapScalingMode(barcodeImg,BitmapScalingMode.NearestNeighbor);
        stack.Children.Add(barcodeImg);
        stack.Children.Add(Txt(product.Code,11,true));
        stack.Children.Add(Txt(product.Name,productFontSize,true));
        stack.Children.Add(Txt($"تاريخ الإنتاج: {production:dd/MM/yyyy}",10,true));
        stack.Children.Add(Txt(showExpiry ? $"الصلاحية: {days} يوم   |   الانتهاء: {expiry:dd/MM/yyyy}" : $"الصلاحية: {days} يوم",9,true));
        root.Children.Add(stack);return root;
    }
    private static BitmapSource BarcodeBitmap(string code)
    {
        var writer=new BarcodeWriterPixelData{Format=BarcodeFormat.CODE_128,Options=new EncodingOptions{Width=500,Height=90,Margin=2,PureBarcode=true}};
        var image=writer.Write(code);
        return BitmapSource.Create(image.Width,image.Height,96,96,PixelFormats.Bgra32,null,image.Pixels,image.Width*4);
    }
}
