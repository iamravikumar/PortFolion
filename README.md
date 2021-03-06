# PortFolion
ポートフォリオの推移を記録・可視化するためのアプリケーションです。
## Discription
中・長期投資を前提としています。株式・投信には対応していますが、信用取引・FXには対応していません。株価情報は[無尽蔵](http://mujinzou.com/)さんから拝借しています。
## Demo
![Graph](https://github.com/Houzkin/PortFolion/blob/images/demo03.gif)  
![Graph](https://github.com/Houzkin/PortFolion/blob/images/demo02.gif)

## Usage
### キャッシュフローの扱い
配当、税金などを外部キャッシュフローとして扱うと辻褄合わせが面倒です。編集ウィンドウから余力の値のみを変更することをおすすめします。  
### 株式分割
分割情報は自動更新されません。編集ウィンドウから分割または併合後の保有数を書き込んでください。

## Download
リリースノートは[こちら](https://github.com/Houzkin/PortFolion/releases)。  
サンプルデータを同封しています。
実際に使い始める場合はサンプルを削除してから使用してください。  
以後アップデートは、自動生成された「今まで書き込んだデータ」フォルダを実行ファイルと同じフォルダにコピペして使ってください。  
![DownloadDirectory](https://github.com/Houzkin/PortFolion/blob/images/SampleDirectory.JPG)

## Author
[@houzkin](https://twitter.com/houzkin)

## License
http://www.apache.org/licenses/LICENSE-2.0

以下のライブラリを使用しています

* MahApps
* LiveCharts
* TreeListView
* Livet
* InteractiveExtensions
* Antlr
* ExpressionEvalutor
* CsvHelper
* 自作ライブラリ
