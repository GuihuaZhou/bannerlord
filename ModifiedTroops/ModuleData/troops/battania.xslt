<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='battanian_volunteer']"/>
    <xsl:template match="NPCCharacter[@id='battanian_clanwarrior']"/>
    <xsl:template match="NPCCharacter[@id='battanian_trained_warrior']"/>
    <xsl:template match="NPCCharacter[@id='battanian_woodrunner']"/>
    <xsl:template match="NPCCharacter[@id='battanian_skirmisher']"/>
    <xsl:template match="NPCCharacter[@id='battanian_picked_warrior']"/>
    <xsl:template match="NPCCharacter[@id='battanian_oathsworn']"/>
    <xsl:template match="NPCCharacter[@id='battanian_wildling']"/>
    <xsl:template match="NPCCharacter[@id='battanian_veteran_skirmisher']"/>
    <xsl:template match="NPCCharacter[@id='battanian_falxman']"/>
    <xsl:template match="NPCCharacter[@id='battanian_veteran_falxman']"/>
    <xsl:template match="NPCCharacter[@id='battanian_highborn_warrior']"/>
    <xsl:template match="NPCCharacter[@id='battanian_hero']"/>
    <xsl:template match="NPCCharacter[@id='battanian_fian']"/>
    <xsl:template match="NPCCharacter[@id='battanian_fian_champion']"/>
    <xsl:template match="NPCCharacter[@id='battanian_scout']"/>
    <xsl:template match="NPCCharacter[@id='battanian_mounted_skirmisher']"/>
    <xsl:template match="NPCCharacter[@id='battanian_horseman']"/>
    <xsl:template match="NPCCharacter[@id='battanian_raider']"/>
    <xsl:template match="NPCCharacter[@id='battanian_highborn_youth']"/>

</xsl:stylesheet>