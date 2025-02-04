﻿#region Copyright and License
// Copyright 2010..2023 Alexander Reinert
// 
// This file is part of the ARSoft.Tools.Net - C# DNS client/server and SPF Library (https://github.com/alexreinert/ARSoft.Tools.Net)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ARSoft.Tools.Net.Dns
{
	public abstract class DnsRecordMessageBase : DnsMessageBase
	{
		private List<DnsQuestion> _questions = new List<DnsQuestion>();
		private List<DnsRecordBase> _answerRecords = new List<DnsRecordBase>();

		/// <summary>
		///   Gets or sets the entries in the question section
		/// </summary>
		public List<DnsQuestion> Questions
		{
			get { return _questions; }
			set { _questions = (value ?? new List<DnsQuestion>()); }
		}

		/// <summary>
		///   Gets or sets the entries in the answer records section
		/// </summary>
		public List<DnsRecordBase> AnswerRecords
		{
			get { return _answerRecords; }
			set { _answerRecords = (value ?? new List<DnsRecordBase>()); }
		}

		/// <summary>
		///   Gets or sets the entries in the authority records section
		/// </summary>
		public List<DnsRecordBase> AuthorityRecords { get; set; } = new();

		protected override void ParseSections(IList<byte> data, ref int currentPosition)
		{
			int questionCount = ParseUShort(data, ref currentPosition);
			int answerRecordCount = ParseUShort(data, ref currentPosition);
			int authorityRecordCount = ParseUShort(data, ref currentPosition);
			int additionalRecordCount = ParseUShort(data, ref currentPosition);

			ParseQuestionSection(data, ref currentPosition, Questions, questionCount);
			ParseRecordSection(data, ref currentPosition, AnswerRecords, answerRecordCount);
			ParseRecordSection(data, ref currentPosition, AuthorityRecords, authorityRecordCount);
			ParseRecordSection(data, ref currentPosition, AdditionalRecords, additionalRecordCount);
		}

		protected override int GetSectionsMaxLength()
		{
			return 12
			       + Questions.Sum(question => question.MaximumLength)
			       + AnswerRecords.Sum(record => record.MaximumLength)
			       + AuthorityRecords.Sum(record => record.MaximumLength)
			       + AdditionalRecords.Sum(record => record.MaximumLength);
		}

		protected override void EncodeSections(IList<byte> messageData, ref int currentPosition, Dictionary<DomainName, ushort> domainNames)
		{
			EncodeUShort(messageData, ref currentPosition, (ushort) Questions.Count);
			EncodeUShort(messageData, ref currentPosition, (ushort) AnswerRecords.Count);
			EncodeUShort(messageData, ref currentPosition, (ushort) AuthorityRecords.Count);
			EncodeUShort(messageData, ref currentPosition, (ushort) AdditionalRecords.Count);

			foreach (DnsQuestion question in Questions)
			{
				question.Encode(messageData, ref currentPosition, domainNames);
			}

			foreach (DnsRecordBase record in AnswerRecords)
			{
				record.Encode(messageData, ref currentPosition, domainNames);
			}

			foreach (DnsRecordBase record in AuthorityRecords)
			{
				record.Encode(messageData, ref currentPosition, domainNames);
			}

			foreach (DnsRecordBase record in AdditionalRecords)
			{
				record.Encode(messageData, ref currentPosition, domainNames);
			}
		}

		internal override bool ValidateResponse(DnsMessageBase responseBase)
		{
			if (responseBase is not DnsRecordMessageBase response)
				return false;

			if (TransactionID != response.TransactionID)
				return false;

			if (response.ReturnCode is ReturnCode.NoError or ReturnCode.NxDomain)
			{
				if ((Questions.Count != response.Questions.Count))
					return false;

				for (var j = 0; j < Questions.Count; j++)
				{
					var queryQuestion = Questions[j];
					var responseQuestion = response.Questions[j];

					if ((queryQuestion.RecordClass != responseQuestion.RecordClass)
					    || (queryQuestion.RecordType != responseQuestion.RecordType)
					    || (!queryQuestion.Name.Equals(responseQuestion.Name, false)))
					{
						return false;
					}
				}
			}

			return true;
		}

		protected internal override void Add0x20Bits()
		{
			Questions = Questions.Select(q => new DnsQuestion(q.Name.Add0x20Bits(), q.RecordType, q.RecordClass)).ToList();
		}

		protected internal override void AddSubsequentResponse(DnsMessageBase response)
		{
			AnswerRecords.AddRange(((DnsRecordMessageBase) response).AnswerRecords);
		}

		protected internal override bool AllowMultipleResponses => IsQuery == false && (Questions.Count == 0 || Questions[0].RecordType is RecordType.Axfr or RecordType.Ixfr);

		protected internal override IEnumerable<DnsMessageBase> SplitResponse() => new SplitResponseEnumerable(this);

		private class SplitResponseEnumerable : IEnumerable<DnsMessageBase>
		{
			private class SplitResponseEnumerator : IEnumerator<DnsMessageBase>
			{
				private readonly List<DnsRecordBase> _answerRecords;
				private int _recordOffset;
				private readonly DnsRecordMessageBase _current;

				public SplitResponseEnumerator(DnsRecordMessageBase response)
				{
					_current = response;
					_answerRecords = response.AnswerRecords;
				}

				public DnsMessageBase Current => _current;

				object IEnumerator.Current => Current;

				public void Dispose()
				{
					_current.AnswerRecords = _answerRecords;
				}

				public bool MoveNext()
				{
					if (_recordOffset >= _answerRecords.Count)
						return false;

					var batch = new List<DnsRecordBase>();
					var maxBatchOffset = Math.Min(_recordOffset + 100, _answerRecords.Count);
					var batchRecordMaxLength = 0;
					for (; _recordOffset < maxBatchOffset && batchRecordMaxLength < 32000; _recordOffset++)
					{
						var record = _answerRecords[_recordOffset];
						batchRecordMaxLength += record.MaximumLength;
						batch.Add(record);
					}

					_current.AnswerRecords = batch;
					return true;
				}

				public void Reset() { }
			}

			private readonly DnsRecordMessageBase _response;

			public SplitResponseEnumerable(DnsRecordMessageBase response)
			{
				_response = response;
			}

			public IEnumerator<DnsMessageBase> GetEnumerator()
			{
				return new SplitResponseEnumerator(_response);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}
	}
}