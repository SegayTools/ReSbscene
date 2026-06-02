新建两个子Agent名字叫SbSceneAgent和SurfboardAgent做对抗式校验， 分别做以下事情:

SbSceneAgent:
读取F:\resbscene\docs所有内容并将各个部分询问SurfboardAgent这部分内容是否一致，如果不一致则通过mcp idapro去检查代码进行辩论，如果代码证明自己是错的则修改文档和代码

SurfboardAgent:
读取C:\Users\mikir\Documents\Tencent Files\664659548\FileRecv\sbscene\sbscene所有内容并验证由SbSceneAgent提出的问题和内容是否正确，假设你是客观上对的一方。


以上对抗过程内容简要和结果均写道F:\resbscene\docs新的文档agent-check-result.md