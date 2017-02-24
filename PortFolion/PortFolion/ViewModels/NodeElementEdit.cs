﻿using Houzkin;
using Houzkin.Architecture;
using Houzkin.Tree;
using Livet;
using Livet.Commands;
using PortFolion.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows;
using System.Collections.Specialized;
using Livet.Messaging;

namespace PortFolion.ViewModels {
	public class AccountEditVM : DynamicViewModel<AccountNode> {
		private InteractionMessenger _messenger;
		public InteractionMessenger Messenger {
			get { return _messenger = _messenger ?? new InteractionMessenger(); }
			set { _messenger = value; }
		}
		public AccountEditVM(AccountNode an) : base(an) {
			var cash = Model.Children.FirstOrDefault(a => a.GetType() == typeof(FinancialValue)) as FinancialValue;
			if(cash == null) {
				cash = new FinancialValue();
				string name = "余力";
				int num = 1;
				while(Model.Children.Any(a=>a.Name == name)) {
					name = "余力" + num;
					num++;
				}
				cash.Name = name;
				Model.AddChild(cash);
			}

			resetElements();
			Elements.CollectionChanged += (s, e) => ChangedTemporaryAmount();
			DummyStock = new StockEditVM(this);
			DummyProduct = new ProductEditVM(this);
		}
		public ObservableCollection<CashEditVM> Elements { get; } = new ObservableCollection<CashEditVM>();
		public DateTime CurrentDate
			=> (Model.Root() as TotalRiskFundNode).CurrentDate;
		public string TemporaryAmount {
			get {
				return Elements.Sum(a => ResultWithValue.Of<double>(double.TryParse, a.Amount).Value).ToString("#.##");
			}
		}
		public void ChangedTemporaryAmount() { OnPropertyChanged(nameof(TemporaryAmount)); }

		StockEditVM _dummyStock;
		public StockEditVM DummyStock {
			get { return _dummyStock; }
			set {
				if (_dummyStock == value) return;
				var tmp = (_dummyStock as INotifyDataErrorInfo);
				if(tmp != null) tmp.ErrorsChanged -= TempS_ErrorsChanged;
				_dummyStock = value;
				tmp = _dummyStock;
				if (tmp != null) tmp.ErrorsChanged += TempS_ErrorsChanged;
			}
		}
		private void TempS_ErrorsChanged(object sender, DataErrorsChangedEventArgs e)
			=> AddStock.RaiseCanExecuteChanged();

		ViewModelCommand addStockCmd;
		public ViewModelCommand AddStock 
			=> addStockCmd = addStockCmd ?? new ViewModelCommand(executeAddStock, canAddStock);
		bool canAddStock() {
			return !DummyStock.HasErrors;// && Elements.All(a => a.Name != DummyStock.Name);//&& Elements.Where(a=>a.IsStock).All(a => a.Name != DummyStock.Name);
		}
		void executeAddStock() {
			DummyStock.Apply();
			Elements.Add(DummyStock);
			DummyStock = new StockEditVM(this);
			OnPropertyChanged(nameof(DummyStock));
		}

		ProductEditVM _dummyProduct;
		public ProductEditVM DummyProduct {
			get { return _dummyProduct; }
			set {
				if (_dummyProduct == value) return;
				var tmp = (_dummyProduct as INotifyDataErrorInfo);
				if(tmp != null) tmp.ErrorsChanged -= TmpP_ErrorsChanged;
				_dummyProduct = value;
				tmp = _dummyProduct;
				if (tmp != null) tmp.ErrorsChanged += TmpP_ErrorsChanged;
			}
		}
		private void TmpP_ErrorsChanged(object sender, DataErrorsChangedEventArgs e)
			=> AddProduct.RaiseCanExecuteChanged();

		ViewModelCommand addProductCommand;
		public ViewModelCommand AddProduct
			=> addProductCommand = addProductCommand ?? new ViewModelCommand(executeAddProduct, canAddProduct);
		bool canAddProduct() {
			return !DummyProduct.HasErrors;// && Elements.Where(a=>a.IsProduct).All(a => a.Name != DummyProduct.Name);
		}
		void executeAddProduct() {
			DummyProduct.Apply();
			Elements.Add(DummyProduct);
			DummyProduct = new ProductEditVM(this);
			OnPropertyChanged(nameof(DummyProduct));
		}

		ViewModelCommand applyCmd;
		public ICommand Apply => applyCmd = applyCmd ?? new ViewModelCommand(apply, canApply);
		bool canApply()
			=> Elements.All(a => !a.HasErrors);
		void apply() {
			var elems = Elements.Where(a => !a.IsRemoveElement || Model.Children.Contains(a.Model)).ToArray();
			var csh = elems.Where(a => a.IsCash);
			var stc = elems.Where(a => a.IsStock).OfType<StockEditVM>().OrderBy(a => a.Code);
			var prd = elems.Where(a => a.IsProduct).OrderBy(a => a.Name);

			var ary = csh.Concat(stc).Concat(prd).ToArray();
			var rmv = Model.Children.Except(ary.Select(a => a.Model));

			rmv.ForEach(a => Model.Children.Remove(a));
			ary.ForEach((ele, idx) => {
				if (Model.Children.Contains(ele.Model)) {
					int oidx = Model.Children.IndexOf(ele.Model);
					if(oidx != idx)
						Model.Children.Move(oidx, idx);
				}else {
					Model.Children.Insert(idx, ele.Model);
				}
			});
		}

		ViewModelCommand allSellCmd;
		public ICommand AllSell => allSellCmd = allSellCmd ?? new ViewModelCommand(allsell);
		void allsell() {
			var elem = Elements.OfType<ProductEditVM>();
			var ec = Elements.First(a => a.IsCash);

			var unrePL = elem.Sum(a => ResultWithValue.Of<double>(double.TryParse, a.Amount).Value);
			var cam = ResultWithValue.Of<double>(double.TryParse, ec.Amount).Value;
			var civ = ResultWithValue.Of<double>(double.TryParse, ec.InvestmentValue).Value;
			ec.InvestmentValue = ((-1D * civ) + (-1D * cam)).ToString();
			ec.Amount = "0";

			foreach(var e in elem) {
				var am = ResultWithValue.Of<double>(double.TryParse, e.Amount).Value;
				var iv = ResultWithValue.Of<double>(double.TryParse, e.InvestmentValue).Value;
				var q = ResultWithValue.Of<double>(double.TryParse, e.Quantity).Value;
				var tq = ResultWithValue.Of<double>(double.TryParse, e.TradeQuantity).Value;
				e.TradeQuantity = ((-1D * tq) + (-1D * q)).ToString();
				e.InvestmentValue = ((-1D * iv) + (-1D * am)).ToString();
				e.Quantity = "0";
				e.Amount = "0";
			}
			if(canApply()) apply();
		}

		ViewModelCommand cancelCml;
		public ICommand Cancel => cancelCml = cancelCml ?? new ViewModelCommand(resetElements);
		void resetElements() {
			Elements.Clear();
			Model.Children.Select(a => {
				var t = a.GetType();
				if (t == typeof(StockValue)) return new StockEditVM(this, a as StockValue);
				else if (t == typeof(FinancialProduct)) return new ProductEditVM(this, a as FinancialProduct);
				else return new CashEditVM(this, a as FinancialValue);
			}).ForEach(a => Elements.Add(a));
		}
		ViewModelCommand applyCurrentPerPrice;
		public ViewModelCommand ApplyCurrentPerPrice {
			get {
				if(applyCurrentPerPrice == null) {
					applyCurrentPerPrice = new ViewModelCommand(applyCurrentPrice, canApplyCurrentPrice);
					Elements.CollectionChanged += (o, e) => applyCurrentPerPrice.RaiseCanExecuteChanged();
				}
				return applyCurrentPerPrice;
			}
		}
		void applyCurrentPrice() {
			var pfs = Elements.OfType<StockEditVM>();
			if (!pfs.Any()) return;
			var ary = Web.KdbDataClient.AcqireStockInfo(this.CurrentDate).ToArray();
			var dic = new List<Tuple<string, string>>();
			
			foreach(var p in pfs) {
				var sym = ary.Where(a => a.Symbol == p.Code).OrderBy(a => a.Turnover).LastOrDefault();
				if (sym == null) {
					dic.Add(new Tuple<string, string>(p.Code, p.Name));
				} else {
					p.CurrentPerPrice = sym.Close.ToString("#.##");
				}
			}
			if (dic.Any()) {
				string msg = "以下の銘柄は値を更新できませんでした。";
				var m = dic.Aggregate(msg, (seed, ele) => seed + "\n" + ele.Item1 + " - " + ele.Item2);
				MessageBox.Show(m, "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
			}
		}
		bool canApplyCurrentPrice()
			=> Elements.Where(a => a.IsStock).All(a => !a.HasErrors);
	}
	
	public class CashEditVM : DynamicViewModel<FinancialValue> {
		protected readonly AccountEditVM AccountVM;
		public CashEditVM(AccountEditVM ac, FinancialValue fv) : base(fv) {
			AccountVM = ac;
			_name = fv.Name;
			_InvestmentValue = fv.InvestmentValue.ToString();
			_Amount = fv.Amount.ToString();
			MenuList.Add(new MenuItemVm(editName) { Header = "名前の変更" });
			MenuList.Add(new MenuItemVm(del) { Header = "削除" });
		}
		void editName() {
			var edi = new NodeNameEditerVM(Model.Parent, Model);

		}
		void del() {
			if (IsCash) {
				MessageBox.Show("このポジションは削除できません。", "削除不可", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (Model.IsRoot()) {
				if (this.IsRemoveElement)
					AccountVM.Elements.Remove(this);
				else
					MessageBox.Show("ポジションが解消されていないため削除できません。\n数量と評価額が「０」の状態で有効です。", "削除不可", MessageBoxButton.OK, MessageBoxImage.Information);
			}else {
				var elems = RootCollection.GetNodeLine(Model.Path, AccountVM.CurrentDate)
					.Select(a => new { Key = a.Key, Value = a.Value as FinancialProduct })
					.LastOrDefault(a => a.Value != null && a.Key < AccountVM.CurrentDate)?.Value;
				if(elems == null || (elems.Amount == 0 && elems.Quantity == 0)) {
					AccountVM.Elements.Remove(this);
				} else {
					MessageBox.Show("前回記入時から継続するポジションは削除できません。\n数量と評価額を「０」にした場合、次回の書き込み時に消滅または削除が可能となります。", "削除不可", MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}
		}
		public virtual void Apply() {
			Model.Name = Name.Trim();
			Model.SetInvestmentValue((long)_investmentValue);
			Model.SetAmount((long)_amount);
		}
		public bool IsCash => GetType() == typeof(CashEditVM);
		public bool IsStock => GetType() == typeof(StockEditVM);
		public bool IsProduct => GetType() == typeof(ProductEditVM);
		public virtual bool IsRemoveElement => false;
		public new FinancialValue Model => base.Model;
		public ObservableCollection<MenuItemVm> MenuList { get; } = new ObservableCollection<MenuItemVm>();
		string _name;
		public string Name {
			get { return _name; }
			set { SetProperty(ref _name, value, nameVali); }
		}
		string nameVali(string param) {
			var nm = param.Trim();
			if (AccountVM.Elements.Contains(this)) {
				if (1 < AccountVM.Elements.Count(a => a.Name == nm))
					return "重複があります";
			} else {
				if (AccountVM.Elements.Any(a => a.Name == nm))
					return "重複があるため追加不可";
			}
			return null;
		}
		protected string _InvestmentValue;
		protected double _investmentValue => ResultWithValue.Of<double>(double.TryParse, _InvestmentValue).Value;
		public virtual string InvestmentValue {
			get { return _InvestmentValue; }
			set {
				if(SetProperty(ref _InvestmentValue, value)) {
					Amount = Model.Amount + _InvestmentValue;
				}
			}
		}
		protected string _Amount;
		protected double _amount => ResultWithValue.Of<double>(double.TryParse, _Amount).Value;
		public virtual string Amount {
			get { return _Amount; }
			set {
				SetProperty(ref _Amount, value);
				OnPropertyChanged(nameof(IsRemoveElement));
				if (AccountVM.Elements.Contains(this)) AccountVM.ChangedTemporaryAmount();
			}
		}
	}
	public class ProductEditVM : CashEditVM {
		public ProductEditVM(AccountEditVM ac, FinancialProduct fp) : base(ac, fp) {
			_TradeQuantity = fp.TradeQuantity.ToString();
			_CurrentPerPrice = fp.Quantity != 0 ? (fp.Amount / fp.Quantity).ToString() : "";
			_Quantity = fp.Quantity.ToString();
		}
		public override void Apply() {
			base.Apply();
			Model.SetTradeQuantity((long)_tradeQuantity);
			Model.SetQuantity((long)_quantity);
		}
		public ProductEditVM(AccountEditVM ac) : base(ac, new FinancialValue()) { }
		public override bool IsRemoveElement => _amount == 0 && _quantity == 0;
		public new FinancialProduct Model => base.Model as FinancialProduct;
		protected string _TradeQuantity;
		protected double _tradeQuantity => ResultWithValue.Of<double>(double.TryParse, _TradeQuantity).Value;
		public virtual string TradeQuantity {
			get { return _TradeQuantity; }
			set {
				if(SetProperty(ref _TradeQuantity, value)) {
					Quantity = (Model.Quantity + _tradeQuantity).ToString();
				}
			}
		}
		public override string InvestmentValue {
			get { return base.InvestmentValue; }
			set {
				if (SetProperty(ref _InvestmentValue, value)) {
					Amount = (Model.Amount + (_tradeQuantity * _investmentValue)).ToString();
				}
			}
		}
		protected string _CurrentPerPrice;
		protected double _currentPerPrice => ResultWithValue.Of<double>(double.TryParse, _CurrentPerPrice).Value;
		public virtual string CurrentPerPrice {
			get { return _CurrentPerPrice; }
			set {
				if(SetProperty(ref _CurrentPerPrice, value)) {
					Amount = (_quantity * _currentPerPrice).ToString();
				}
			}
		}
		protected string _Quantity;
		protected double _quantity => ResultWithValue.Of<double>(double.TryParse, _Quantity).Value;
		public virtual string Quantity {
			get { return _Quantity; }
			set {
				if(SetProperty(ref _Quantity, value)) 
					Amount = (_quantity * _currentPerPrice).ToString();
			}
		}

	}
	public class StockEditVM: ProductEditVM {
		public StockEditVM(AccountEditVM ac, StockValue sv) : base(ac, sv) {
			_Code = sv.Code.ToString();
		}
		public StockEditVM(AccountEditVM ac) : base(ac, new StockValue()) { }
		public new StockValue Model => base.Model as StockValue;
		protected string _Code;
		public string Code {
			get { return _Code; }
			set {
				if (SetProperty(ref _Code, value, codeValidate))
					AccountVM.ApplyCurrentPerPrice.RaiseCanExecuteChanged();
			}
		}
		string codeValidate(string value) {
			var r = ResultWithValue.Of<int>(int.TryParse, value);
			if (!r) return "コードを入力してください";
			if (value.Count() != 4) return "4桁";
			var d = Model.Upstream().OfType<TotalRiskFundNode>().Last().CurrentDate;
			var tgh = Web.KdbDataClient
				.AcqireStockInfo(d)
				.Where(a => int.Parse(a.Symbol) == r.Value).ToArray();
			if (!tgh.Any()) return "";
			var tg = tgh.OrderBy(a => a.Turnover).Last();
			this.CurrentPerPrice = tg.Close.ToString("#.##");
			if (string.IsNullOrEmpty(this.Name) || string.IsNullOrWhiteSpace(this.Name))
				this.Name = tg.Name;
			return null;
		}
		
		public override void Apply() {
			base.Apply();
			Model.Code = ResultWithValue.Of<int>(int.TryParse, _Code).Value;
		}
	}
}