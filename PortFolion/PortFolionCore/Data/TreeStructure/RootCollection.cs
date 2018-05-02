﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Houzkin.Tree;
using PortFolion.IO;
using Houzkin.Collections;
using System.Collections.Specialized;
using Houzkin;

namespace PortFolion.Core {
	
	public class RootCollection : ObservableCollection<TotalRiskFundNode> {


		public static RootCollection Instance { get; } = new RootCollection();

		public static void ReRead(IEnumerable<TotalRiskFundNode> roots){
			var ds = roots.Select(a => a.CurrentDate).ToArray();
			foreach (var n in roots) Instance.Remove(n);
			foreach(var n in HistoryIO.ReadRoots(ds)) Instance.Add(n);
		}
		public static TotalRiskFundNode GetOrCreate(DateTime date) {
			date = new DateTime(date.Year, date.Month, date.Day);
			if (!Instance.Keys.Contains(date)) {
				var ins = Instance.Keys.Where(a => a < date);
				var pns = Instance.Keys.Where(a => a > date);
				if (!ins.Any() && !pns.Any()) {
					var r = new TotalRiskFundNode();
					r.CurrentDate = date;
					Instance.Add(r);
				}else if(ins.Any()) {
					var d = ins.Max();
					var a1 = Instance[d] as CommonNode;
					var trfn = a1.Convert(a => a.Clone(), (a, b) => a.AddChild(b)) as TotalRiskFundNode;
					var rl = trfn.Levelorder().Skip(1).Where(a => !a.HasPosition && !a.HasTrading).ToArray();
					foreach (var r in rl) r.MaybeRemoveOwn();
					//trfn.RemoveDescendant(a => !a.HasPosition && !a.HasTrading);
					trfn.CurrentDate = date;
					Instance.Add(trfn);
				}else if (pns.Any()) {
					var d = pns.Min();
					var trfn = ((Instance[d] as CommonNode).Convert(a => a.Clone(),(a,b)=>a.AddChild(b))) as TotalRiskFundNode;
					foreach(var nd in trfn.Levelorder().OfType<FinancialValue>()) {
						nd.SetAmount(0);
						var ndp = nd as FinancialProduct;
						if (ndp != null) ndp.SetQuantity(0);
					}
					trfn.CurrentDate = date;
					Instance.Insert(0, trfn);
				}
			}
			return Instance[date];
		}
		/// <summary>指定した位置に存在するノードを全て取得する。</summary>
		/// <param name="path">位置を示すパス</param>
		public static Dictionary<DateTime,CommonNode> _GetNodeLine(IEnumerable<string> path) {
			return GetNodeLine(path, a => a);
		}
		public static Dictionary<DateTime,T> GetNodeLine<T>(IEnumerable<string> path,Func<CommonNode,T> elementSelector){
			/*未テスト
			return Instance
				.SelectMany(
					a => a.Evolve(
						b => b.IsRoot() ? b.Name == path.ElementAtOrDefault(0) ? b.Children : null : b.Children,
						(b, c) => b.Concat(c.Where(d => d.Name == path.ElementAtOrDefault(d.Upstream().Count() - 1))))
					.Where(b=>b.Path.SequenceEqual(path)))//Rootを除けない
				.ToDictionary(a => (a.Root() as TotalRiskFundNode).CurrentDate);
			*/
			return Instance.SelectMany(
					a => a.Evolve(
						b => b.Path.Except(path).Any() ? null : b.Children,
						(c, d) => c.Concat(d)))
				.Where(e => e.Path.SequenceEqual(path))
				.ToDictionary(b => (b.Root() as TotalRiskFundNode).CurrentDate, b => elementSelector(b));
		}
		/// <summary>指定した時間において、指定した位置に存在するノードを取得する</summary>
		public static CommonNode GetNode(IEnumerable<string> path, DateTime date) {
			var nn = _GetNodeLine(path);//.ToDictionary(a => (a.Root() as TotalRiskFundNode).CurrentDate);
			return nn.LastOrDefault(a => a.Key <= date).Value;
		}
		
		/// <summary>指定した時間を含む指定位置のポジション単位のノードを取得する</summary>
		public static Dictionary<DateTime,CommonNode> _GetNodePosition(IEnumerable<string> path, DateTime currentTenure) {
			var lne = _GetNodeLine(path);
			//指定した日付を含んだノードを取得
			var curNd = lne.LastOrDefault(a => currentTenure <= a.Key);
			if (curNd.Value == null) return new Dictionary<DateTime, CommonNode>();
			
			var bef = lne.TakeWhile(a => a.Value != curNd.Value).Reverse().TakeWhile(a => a.Value.HasPosition);
			var aft = lne.SkipWhile(a => a.Value != curNd.Value).Separate(a => !a.Value.HasPosition).First();

			return bef.Concat(aft).OrderBy(a => a.Key).ToDictionary(a => a.Key, b => b.Value);
		}
		/// <summary>各ルートごとに指定した条件を満たすノードを取得する</summary>
		/// <typeparam name="T">取得する型</typeparam>
		/// <param name="pred">条件</param>
		public static Dictionary<DateTime,IEnumerable<CommonNode>> _GetAllPositions<T>(Func<T,bool> pred) where T : CommonNode{
			var dic = new Dictionary<DateTime, IEnumerable<CommonNode>>();
			foreach(var n in RootCollection.Instance){
				var ns = n.Preorder().OfType<T>().Where(a => pred(a));
				if (ns.Any()) dic.Add(n.CurrentDate, ns);
			}
			return dic;
		}
		/// <summary>各ルートごとにノードを取得する</summary>
		/// <param name="name">ノード名</param>
		/// <param name="type">ノードの型</param>
		public static Dictionary<DateTime,IEnumerable<CommonNode>> _GetAllPositions(string name,NodeType type){
			return _GetAllPositions<CommonNode>(a => a.GetNodeType() == type && a.Name == name);
		}
		/// <summary>指定したパスに該当する全てのノードの名前を変更可能かどうか示す値を取得する。</summary>
		/// <param name="path">変更対象を示すパス</param>
		/// <param name="name">新しい名前</param>
		/// <returns>重複があった日付</returns>
		[Obsolete]
		public static ResultWithValue<IEnumerable<DateTime>> CanChangeNodeName(IEnumerable<string> path,string name) {
			var src = _GetNodeLine(path)
				.Select(a => new { Date = a.Key, Sigls = a.Value.Siblings().Except(new CommonNode[] { a.Value }) })
				.Select(a => new { Date = a.Date, Rst = a.Sigls.All(b => b.Name != name) })
				.ToArray();
			if (src.All(a => a.Rst)) {
				return new ResultWithValue<IEnumerable<DateTime>>(true, Enumerable.Empty<DateTime>());
			}else {
				return new ResultWithValue<IEnumerable<DateTime>>(false, src.Where(a => !a.Rst).Select(a => a.Date));
			}
		}
		/// <summary>指定したパスに該当する全てのノード名を変更する</summary>
		/// <param name="path">変更対象を示すパス</param>
		/// <param name="newName">新しい名前</param>
		/// <returns>変更を行った日付</returns>
		[Obsolete]
		public static IEnumerable<DateTime> ChangeNodeName(IEnumerable<string> path,string newName) {
			List<DateTime> lst = new List<DateTime>();
			foreach (var t in _GetNodeLine(path)) {
				if (t.Value.Siblings().All(a => a.Name != newName)) {
					t.Value.Name = newName;
					lst.Add(t.Key);
				}
			}
			return lst;
		}
		
		public static ResultWithValue<IEnumerable<KeyValuePair<DateTime,CommonNode>>> CanChangeNodeName(CommonNode node,string name, TagEditParam param){
			name = name.Trim();
			var dt = (node.Root() as TotalRiskFundNode).CurrentDate;
			//現在(変更前)のノード名で検索
			var hso = RootCollection._GetNodeLine(node.Path);
			switch (param) {
			case TagEditParam.AllHistory:
				var lh = hso.Where(a => !a.Value.CanChangeName(name));
				if (lh.Any()) return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(false, lh);
				else return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(true, hso);// Enumerable.Empty<KeyValuePair<DateTime,CommonNode>>());
			case TagEditParam.FromCurrent:
				var fc = hso.SkipWhile(a => a.Key < dt);
				var fcc = fc.Where(a => !a.Value.CanChangeName(name));
				if (fcc.Any()) return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(false, fcc.ToArray());
				else return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(true, fc.ToArray());// Enumerable.Empty<KeyValuePair<DateTime, CommonNode>>());
			case TagEditParam.Position:
				var ps = RootCollection._GetNodePosition(node.Path, dt);
				var pss = ps.Where(a => !a.Value.CanChangeName(name));
				if (pss.Any()) return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(false, pss.ToArray());
				else return new ResultWithValue<IEnumerable<KeyValuePair<DateTime, CommonNode>>>(true, ps.ToArray());// Enumerable.Empty<KeyValuePair<DateTime, CommonNode>>());
			default:
				throw new ArgumentException();
			}
		}
		public static IEnumerable<CommonNode> ChangeNodeName(CommonNode node, string newName,TagEditParam param){
			newName = newName.Trim();
			var dt = (node.Root() as TotalRiskFundNode).CurrentDate;
			return CanChangeNodeName(node, newName, param).TrueOrNot(
				o => {
					foreach (var p in o.Select(a => a.Value)) {
						p.ChangeName(newName);
						(p.Parent as FinancialBasket)?.SortChildren();
					}
					return o.Select(a => a.Value);
				}, x => Enumerable.Empty<CommonNode>());
		}

		public static IEnumerable<CommonNode> ChangeTag(CommonNode node,string tagName, TagEditParam param){
			var lst = new List<CommonNode>();
			var tg = TagInfo.GetWithAdd(tagName);
			IEnumerable<KeyValuePair<DateTime, CommonNode>> func() {
				switch (param) {
				case TagEditParam.AllHistory:
					return RootCollection._GetNodeLine(node.Path);
				case TagEditParam.Position:
					return RootCollection._GetNodePosition(node.Path, (node.Root() as TotalRiskFundNode).CurrentDate);
				case TagEditParam.FromCurrent:
					return RootCollection._GetNodeLine(node.Path)
						.SkipWhile(a => a.Key < (node.Root() as TotalRiskFundNode).CurrentDate);
				default:
					throw new ArgumentException();
				}
			}
			foreach (var d in func()){
				d.Value.Tag = tg;
				lst.Add(d.Value);
			}
			return lst;
		}

		#region インスタンス
		private RootCollection() :base() {
			var itm = HistoryIO.ReadRoots().OrderBy(a => a.CurrentDate);
			foreach (var i in itm) this.Items.Add(i);
		}
		public void Refresh() {
			
			this.Items.Clear();
			
			var itm = HistoryIO.ReadRoots().OrderBy(a => a.CurrentDate);
			foreach (var i in itm) this.Items.Add(i);

			this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
		internal void DateTimeChange(DateTime date) {
			var lst = new List<DateTime>(this.Keys);
			var cur = lst.IndexOf(date);
			var idx = lst.FindIndex(a => date < a);
			if (cur == idx) return;
			this.Move(cur, idx);
		}
		bool checkSeq(int index) {
			if (0 < index && this[index - 1].CurrentDate > this[index].CurrentDate) return false;
			if (index < Count-1 && this[index].CurrentDate > this[index + 1].CurrentDate) return false;
			return true;
		}
		protected override void InsertItem(int index, TotalRiskFundNode item) {
			if (ContainsKey(item.CurrentDate)) return;
			var hs = new HashSet<DateTime>(this.Keys);
			hs.Add(item.CurrentDate);
			var idx = hs.OrderBy(a => a).Select((d, i) => new { d, i }).First(a => a.d == item.CurrentDate).i;// new List<DateTime>(hs.OrderBy(a => a)).IndexOf(item.CurrentDate);
			base.InsertItem(idx, item);
		}
		protected override void SetItem(int index, TotalRiskFundNode item) {
			if (ContainsKey(item.CurrentDate)) return;
			this[index].MainList = null;
			base.SetItem(index, item);
			if (!checkSeq(index)) DateTimeChange(item.CurrentDate);
		}
		protected override void RemoveItem(int index) {
			this[index].MainList = null;
			base.RemoveItem(index);
		}
		protected override void ClearItems() {
			foreach (var itm in this) itm.MainList = null;
			base.ClearItems();
		}
		public TotalRiskFundNode this[DateTime key] {
			get {
				key = key.Date;
				if (!this.ContainsKey(key)) throw new KeyNotFoundException();
				return Items.First(a => a.CurrentDate == key);
			}
		}

		public IEnumerable<DateTime> Keys {
			get { return Items.Select(a => a.CurrentDate); }
		}

		public bool ContainsKey(DateTime key) {
			key = key.Date;
			return Keys.Contains(key);
		}

		public bool TryGetValue(DateTime key, out TotalRiskFundNode value) {
			key = key.Date; 
			if (ContainsKey(key)) {
				value = Items.First(a => a.CurrentDate == key);
				return true;
			} else {
				value = null;
				return false;
			}
		}
		#endregion
	}
}
