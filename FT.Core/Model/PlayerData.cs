using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    [Serializable]
    public class PlayerData
    {
        public int id;
        public string firstName;
        public string lastName;
        public GameDate dateOfBirth;
        public Nationality nationality;
        public Nationality secondNationality;

        // Position.None = 미지정. 비트 OR로 복수 포지션 표현 (예: Position.LB | Position.LWB)
        public Position positionFlags;
        public Position preferredPosition;
        public PreferredFoot preferredFoot;

        public int height;   // cm
        public int weight;   // kg

        public TechnicalAttributes technical;
        public PhysicalAttributes physical;
        public MentalAttributes mental;
        public GoalkeeperAttributes goalkeeper;

        public int potential;   // 1~99
        public HiddenAttributes hidden;

        public Personality personality;
        public float morale;     // 0~100
        public float condition;  // 0~100

        public ContractData contract;
        public int marketValue;

        public int clubId;
        public bool isOnLoan;
        public int? loanedFromClubId;

        public PlayerStatus status;
        public TransferStatus transferStatus;

        /// <summary>국가대표 출전 횟수.</summary>
        public int caps;

        /// <summary>국가대표 득점.</summary>
        public int internationalGoals;

        /// <summary>이번 시즌 2군 출전 수.</summary>
        public int reserveAppearances;

        public HomegrownStatus  homegrown;
        public WorkPermitStatus workPermit;

        public RoleAssignment roleAssignment;
        public InjuryData injury;

        public PlayerStats seasonStats;

        /// <summary>통산 기록. 시즌이 넘어갈 때 seasonStats 를 여기에 더한다.</summary>
        public PlayerStats careerStats;
        [JsonIgnore] public List<MatchRating> recentRatings = new List<MatchRating>();

        // 능력치 캐시 — [NonSerialized]이므로 역직렬화 후 첫 접근 시 자동 재계산
        [NonSerialized] private bool  _abilityCacheValid;
        [NonSerialized] private float _cachedAbility;

        /// <summary>화면에 쓰는 이름. 붙이는 방식은 나라를 따른다(NameStyle).</summary>
        [JsonIgnore] public string FullName => NameStyle.Full(nationality, firstName, lastName);

        /// <summary>좁은 칸용 짧은 이름.</summary>
        [JsonIgnore] public string ShortName => NameStyle.Short(nationality, firstName, lastName);
        [JsonIgnore] public bool   IsInjured => injury != null && injury.isActive;

        // Position [Flags] 비트 연산으로 O(1) 포지션 확인
        public bool CanPlay(Position pos) => (positionFlags & pos) != 0;

        public int GetAge(GameDate currentDate)
        {
            int age = currentDate.year - dateOfBirth.year;
            if (currentDate.month < dateOfBirth.month ||
                (currentDate.month == dateOfBirth.month && currentDate.day < dateOfBirth.day))
                age--;
            return age;
        }

        /// <summary>
        /// 능력치 평균(1~20)의 실수값. CA 환산과 성장은 이 값을 쓴다.
        /// 정수로 자르면 1점이 CA 약 18 에 해당해, 한 시즌 성장이 표에 보이지 않는다.
        /// </summary>
        [JsonIgnore]
        public float AbilityScore
        {
            get
            {
                if (!_abilityCacheValid)
                {
                    _cachedAbility = CalculateCurrentAbility();
                    _abilityCacheValid = true;
                }
                return _cachedAbility;
            }
        }

        /// <summary>표시용 정수 전력. 기존 호출부와 매치 엔진 기준을 그대로 유지한다.</summary>
        [JsonIgnore] public int CurrentAbility => (int)AbilityScore;

        // 능력치 수정 후 반드시 호출
        public void InvalidateAbilityCache() => _abilityCacheValid = false;

        // 최근 5경기 평점만 유지
        public void AddMatchRating(MatchRating rating, int maxHistory = 5)
        {
            recentRatings.Add(rating);
            if (recentRatings.Count > maxHistory)
                recentRatings.RemoveAt(0);
        }

        private float CalculateCurrentAbility()
            => (technical.Average() + physical.Average() + mental.Average()) / 3f;
    }

    // ── 공개 능력치 ───────────────────────────────────────────────────────────

    [Serializable]
    public class TechnicalAttributes
    {
        public int passing;
        public int dribbling;
        public int shooting;
        public int finishing;
        public int heading;
        public int crossing;
        public int tackling;
        public int marking;
        public int firstTouch;
        public int longPassing;
        public int setPieces;

        public float Average() => (passing + dribbling + shooting + finishing +
                                   heading + crossing + tackling + marking +
                                   firstTouch + longPassing + setPieces) / 11f;
    }

    [Serializable]
    public class PhysicalAttributes
    {
        public int speed;
        public int acceleration;
        public int stamina;
        public int strength;
        public int jumping;
        public int agility;
        public int balance;

        public float Average() => (speed + acceleration + stamina + strength +
                                   jumping + agility + balance) / 7f;
    }

    [Serializable]
    public class MentalAttributes
    {
        public int vision;
        public int composure;
        public int leadership;
        public int courage;
        public int workRate;
        public int concentration;
        public int decisions;
        public int positioning;
        public int aggression;
        public int teamwork;
        public int offTheBall;

        public float Average() => (vision + composure + leadership + courage +
                                   workRate + concentration + decisions + positioning +
                                   aggression + teamwork + offTheBall) / 11f;
    }

    [Serializable]
    public class GoalkeeperAttributes
    {
        public int handling;
        public int diving;
        public int reflexes;
        public int positioning;
        public int communication;
        public int kicking;
        public int throwing;
        public int oneOnOne;
        public int aerialAbility;
        public int commandArea;

        public float Average() => (handling + diving + reflexes + positioning +
                                   communication + kicking + throwing +
                                   oneOnOne + aerialAbility + commandArea) / 10f;
    }

    // ── 히든 능력치 ───────────────────────────────────────────────────────────

    [Serializable]
    public class HiddenAttributes
    {
        public int consistency;
        public int importantMatches;
        public int pressureHandling;

        public int injuryProneness;
        public int recoveryRate;

        public int professionalism;
        public int ambition;
        public int loyalty;
        public int temperament;
        public int sportsmanship;
        public int dirtiness;

        public int adaptability;
        public int versatility;

        public int controversy;
        public int influence;
    }

    // ── 역할 배정 ─────────────────────────────────────────────────────────────

    [Serializable]
    public class RoleAssignment
    {
        public PlayerRole role;
        public RoleDuty   duty;
        public int        suitability;  // 0~100
    }

    // ── 부상 ─────────────────────────────────────────────────────────────────

    [Serializable]
    public class InjuryData
    {
        public string        injuryType;
        public InjurySeverity severity;
        public GameDate       injuryDate;
        public GameDate       expectedReturnDate;
        public bool          isActive;
    }

}
