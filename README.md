# SAMTool
segment anything（SAM） for C# Inference

官方网址：https://segment-anything.com/
官方项目源码：https://github.com/facebookresearch/segment-anything
虽然官方提供了预训练模型，和推理代码，但预训练模型只有pytorch格式的，且推理代码只提供了基于Pytorch框架的Python代码。

本项目包含两部分：
1.将官方发布的预训练模型，拆分成编码器和解码器，并分别保存为ONNX格式。
2.使用C#语言加载模型，进行推理，并用WPF进行交互和显示。
效果演示视频：
https://weixin.qq.com/sph/A1KT5X
https://weixin.qq.com/sph/AJXH8U

由于Github不支持上传超过25M的文件，所以ONNX模型文件不能上传，如有需要请关注下面微信公众号，后台回复【SAM】

关注微信公众号：**人工智能大讲堂**<br />  
<img width="180" src="https://user-images.githubusercontent.com/18625471/228743333-77abe467-2385-476d-86a2-e232c6482291.jpg"><br /> 
