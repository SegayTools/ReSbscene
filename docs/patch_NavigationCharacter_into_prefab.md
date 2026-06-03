目标:
编写新的C#项目在src文件夹里面，项目名叫NavigationCharacterPatcher，该项目主要是修改指定的prefab ab文件里面的脚本NavigationCharacter assets的m_Script.m_PathID

大概流程:
1. 指定一个ab文件
2. 通过AssetsTools.NET加载这个ab文件，并找到NavigationCharacter的assets文件，修改它的m_Script.m_PathID为1119486627253801066
3. 重新打包成新的ab文件