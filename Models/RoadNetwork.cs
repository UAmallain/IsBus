using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IsBus.Models;

[Table("road_network")]
public class RoadNetwork
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ngd_uid")]
    [StringLength(10)]
    public string? NgdUid { get; set; }

    [Column("name")]
    [StringLength(100)]
    public string? Name { get; set; }

    [Column("type")]
    [StringLength(10)]
    public string? Type { get; set; }

    [Column("direction")]
    [StringLength(2)]
    public string? Direction { get; set; }

    [Column("address_from_left")]
    [StringLength(20)]
    public string? AddressFromLeft { get; set; }

    [Column("address_to_left")]
    [StringLength(20)]
    public string? AddressToLeft { get; set; }

    [Column("address_from_right")]
    [StringLength(20)]
    public string? AddressFromRight { get; set; }

    [Column("address_to_right")]
    [StringLength(20)]
    public string? AddressToRight { get; set; }

    [Column("csd_uid_left")]
    [StringLength(10)]
    public string? CsdUidLeft { get; set; }

    [Column("csd_name_left")]
    [StringLength(100)]
    public string? CsdNameLeft { get; set; }

    [Column("csd_type_left")]
    [StringLength(5)]
    public string? CsdTypeLeft { get; set; }

    [Column("csd_uid_right")]
    [StringLength(10)]
    public string? CsdUidRight { get; set; }

    [Column("csd_name_right")]
    [StringLength(100)]
    public string? CsdNameRight { get; set; }

    [Column("csd_type_right")]
    [StringLength(5)]
    public string? CsdTypeRight { get; set; }

    [Column("province_uid_left")]
    [StringLength(2)]
    public string? ProvinceUidLeft { get; set; }

    [Column("province_name_left")]
    [StringLength(100)]
    public string? ProvinceNameLeft { get; set; }

    [Column("province_uid_right")]
    [StringLength(2)]
    public string? ProvinceUidRight { get; set; }

    [Column("province_name_right")]
    [StringLength(100)]
    public string? ProvinceNameRight { get; set; }

    [Column("road_class")]
    [StringLength(2)]
    public string? RoadClass { get; set; }

    [Column("road_rank")]
    [StringLength(1)]
    public string? RoadRank { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Helper properties
    [NotMapped]
    public string? PrimaryCommunity => CsdNameLeft ?? CsdNameRight;

    [NotMapped]
    public string? PrimaryProvince => ProvinceUidLeft ?? ProvinceUidRight;

    [NotMapped]
    public string? PrimaryProvinceName => ProvinceNameLeft ?? ProvinceNameRight;
}