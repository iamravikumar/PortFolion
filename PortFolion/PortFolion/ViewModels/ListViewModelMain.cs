﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Livet;
using PortFolion.Core;
using Houzkin.Tree;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PortFolion.ViewModels {
	public class ListviewModel : ViewModel {
		RootCollection Model;
		public ListviewModel() {
			Model = RootCollection.Instance;
			Model.CollectionChanged += CollectionChanged;
			totalRiskFund = RootCollection.Instance.LastOrDefault();
			if (totalRiskFund != null) {
				CurrentDate = totalRiskFund.CurrentDate;
				Path = totalRiskFund.Path;
			}else {
				CurrentDate = DateTime.Today;
				Path = Enumerable.Empty<string>();
			}
		}

		private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			var rt = Model.LastOrDefault(a => a.CurrentDate <= this.CurrentDate) ?? Model.LastOrDefault();
			if (totalRiskFund == rt) return;
			if(rt == null) {
				totalRiskFund = null;
				CurrentDate = DateTime.Today;
				Path = Enumerable.Empty<string>();
				Refresh();
				return;
			}else {
				totalRiskFund = rt;
				SetCurrentDate(totalRiskFund.CurrentDate);
			}
		}

		public DateTime CurrentDate { get; private set; }
		TotalRiskFundNode _trfn;
		TotalRiskFundNode totalRiskFund {
			get { return _trfn; }
			set {
				if (value == _trfn) return;
				_trfn = value;
				root = CommonNodeVM.Create(_trfn);
			}
		}
		CommonNodeVM root;
		public CommonNodeVM Root => root;

		public IEnumerable<string> Path { get; private set; }
		public IEnumerable<CommonNodeVM> History
			=> RootCollection.GetNodeLine(new NodePath<string>(Path)).Select(a => CommonNodeVM.Create(a));
		
		public void SetCurrentDate(DateTime date) {
			date = date.Date;
			var c = RootCollection.Instance.LastOrDefault(a => date <= a.CurrentDate) ?? RootCollection.Instance.LastOrDefault();
			if (c == null) {
				if (totalRiskFund == null) {
					CurrentDate = date;//notify
					RaisePropertyChanged(nameof(CurrentDate));
				}
				return;
			}
			totalRiskFund = c;//notify
			CurrentDate = totalRiskFund.CurrentDate;//notify
			
			if (Path.Any() && totalRiskFund.Levelorder().Any(a => a.Path.SequenceEqual(Path))) {
				RaisePropertyChanged(nameof(Root));
				RaisePropertyChanged(nameof(CurrentDate));
				ExpandCurrentNode();
				return;
			}
			Path = totalRiskFund.Levelorder()
				.Select(a => a.Path.Zip(this.Path, (b, d) => new { b, d })
					.TakeWhile(e => e.b == e.d)
					.Select(f => f.b))
				.LastOrDefault() ?? Enumerable.Empty<string>();
			Refresh();
		}
		public void SetPath(IEnumerable<string> path) {
			if (path.SequenceEqual(Path)) return;
			Path = path;

			RaisePropertyChanged(nameof(this.Path));
			RaisePropertyChanged(nameof(this.History));
			RaisePropertyChanged(nameof(this.CurrentDate));
			ExpandCurrentNode();
		}
		public void Refresh() {
			RaisePropertyChanged(nameof(this.Root));
			RaisePropertyChanged(nameof(this.Path));
			RaisePropertyChanged(nameof(this.History));
			RaisePropertyChanged(nameof(this.CurrentDate));
			ExpandCurrentNode();
		}
		#region tree
		public void ExpandCurrentNode() {
			if (!this.Path.Any()) return;
			var c = Root.Levelorder().FirstOrDefault(a => a.Path.SequenceEqual(this.Path));
			foreach (var n in c.Upstream()) n.IsExpand = true;
		}
		public void ExpandAllNode() {
			if (Root != null)
				foreach (var n in Root.Levelorder())
					n.IsExpand = true;
		}
		public void CloseAllNode() {
			if (Root != null)
				foreach (var n in Root.Levelorder())
					n.IsExpand = false;
		}
		#endregion
	}
}