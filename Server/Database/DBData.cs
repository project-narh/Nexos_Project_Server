using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public enum ItemState
{
    USE, TRADE, DESTROY, LOCK
}

public enum ItemType
{
    EQUIPMENT, CONSUMABLE, MATERIAL
}

[Table("user_account")]
public class UserAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("uid")]
    public int Uid { get; set; }

    [Column("user_id")]
    public string UserId { get; set; }

    [Column("user_pw")]
    public string UserPw { get; set; }

    [Column("nickname")]
    public string Nickname { get; set; }
}

[Table("item_list")]
public class ItemList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("state", TypeName = "ENUM('USE','TRADE','DESTROY','LOCK')")]
    //public ItemState State { get; set; }
    public string State { get; set; }

    [Column("type", TypeName = "ENUM('EQUIPMENT','CONSUMABLE','MATERIAL')")]
    public string Type { get; set; }

    [Column("etc")]
    public string Etc { get; set; }
    [Column("image")]
    public string Image { get; set; }
}

[Table("user_item")]
public class UserItem
{
    [Column("id")]
    public int Id { get; set; }

    [Column("uniqueId")]
    public int UniqueId { get; set; } // 아이템 고유번호

    [Column("name")]
    public string Name { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("state")]
    public string State { get; set; }

    [Column("type", TypeName = "ENUM('EQUIPMENT','CONSUMABLE','MATERIAL')")]
    public string Type { get; set; }

    [Column("count")]
    public int Count { get; set; }

    [Column("uid")]
    public int Uid { get; set; }

    [Column("etc")]
    public string Etc { get; set; }
    [Column("image")]
    public string Image { get; set; }
    [Column("tokenId")]
    public int TokenId { get; set; }

    [Column("version")]
    public int Version { get; set; }
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("timestamp")]
    public long Timestamp { get; set; }
}

[Table("shop_list")]
public class ShopList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ShopID")]
    public int shopID { get; set; }

    [Column("Name")]
    public string name { get; set; }
    public virtual ICollection<ShopItem> ShopItems { get; set; }
}

[Table("shop_item")]
public class ShopItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ShopID", Order = 0)]
    public int shopID { get; set; }

    [Column("ItemID", Order = 1)]
    public int itemID { get; set; }

    [ForeignKey("shopID")]
    public virtual ShopList Shop { get; set; }

    // item_list 테이블과의 외래 키 관계 
    [ForeignKey("itemID")]
    public virtual ItemList Item { get; set; }
}

[Table("address_account")]
public class AddressAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("UID", Order = 0)]
    public int UID { get; set; }

    [Column("address", Order = 1)]
    public string address { get; set; }

    [Column("pr_Address", Order = 2)]
    public string pr_Address { get; set; }
}

public class UserItemDTO
{
    public int Id { get; set; }
    public int UniqueId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string State { get; set; }
    public string Type { get; set; }
    public int Count { get; set; }
    public int Uid { get; set; }
    public string Etc { get; set; }
    public string Image { get; set; }

}

